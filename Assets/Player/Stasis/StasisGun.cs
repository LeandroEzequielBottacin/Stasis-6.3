using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Managers.Game;
using Player.Scripts.Interactor;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements;
using Puzzle_Elements.Hedron.Scripts;
using UIScripts.FeedBack_UI.Crosshair;
using UnityEngine;

namespace Player.Stasis
{
    public class StasisGun : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Muzzle/Socket del brazo derecho (FX por defecto).")]
        [SerializeField] private Transform rightStasisOrigin;
        [Tooltip("Muzzle/Socket del brazo izquierdo.")]
        [SerializeField] private Transform leftStasisOrigin;

        [Tooltip("Compat: si lo dejas asignado, se usa como 'derecho' si rightStasisOrigin está vacío.")]
        [SerializeField] private Transform stasisOriginLegacy;

        [SerializeField] private GameObject stasisBeamPrefab;
        [SerializeField] private GameObject particleStasisMissed;

        [Header("Raycast (real desde cámara)")]
        [SerializeField, Tooltip("Radio del SphereCast para perdonar errores de puntería.")]
        private float radiusStasis = 0.2f;
        [SerializeField, Tooltip("Distancia máxima del raycast/spherecast.")]
        private float maxDistance = 300f;
        [SerializeField, Tooltip("Capas objetivo (excluye Player/Arma).")]
        private LayerMask layer;

        [Header("Cámara")]
        [SerializeField, Tooltip("Si está vacío, usa Camera.main en Start.")]
        private UnityEngine.Camera cameraOverride;
        [HideInInspector] public UnityEngine.Camera mainCam;

        [Header("Disparo")]
        [SerializeField] private float cooldown = 0.25f;
        public bool canShootStasis = true;

        [Header("Spam (Dual Berettas style)")]
        [SerializeField, Tooltip("Si los clicks están dentro de esta ventana, alterna la mano.")]
        private float spamWindow = 0.18f;

        [Header("Debug")]
        [SerializeField] private bool debugDraw = true;
        [SerializeField] private bool debugLogs;
        [SerializeField] private float debugPersist = 0.25f;
        [SerializeField] private Color debugRayColor = new Color(0f, 0.9f, 1f);
        [SerializeField] private Color debugBeamColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color debugNormalColor = new Color(1f, 1f, 0f);
        [SerializeField] private Color debugMissColor = new Color(1f, 0.3f, 0.3f);

        private readonly int _maxStasisObjects = 2;
        private readonly List<(GameObject obj, IStasis stasis)> _stasisList = new();

        private StasisBeam _activeBeam;
        private PlayerInteractor _playerInteractor;
        private View _view;
        private Model _model;                 // Model para eventos de wallrun

        public event Action OnShoot = delegate { };

        // Estado interno para spam/alternancia
        private enum Hand { Right, Left }
        private float _lastShotTime = -999f;
        private Hand _lastHandUsed = Hand.Right;

        // Estado de wallrun (decidido por eventos del Model)
        private bool _isWallrunning;
        private Hand _wallOnSide; // lado donde está la pared (Left = pared a la izquierda del jugador)


        private bool _stasisActivate;
        public Transform camHolder;

        private void Start()
        {
            _playerInteractor = GetComponent<PlayerInteractor>();
            _view = GetComponentInParent<View>();
            _model = GetComponentInParent<Model>();           // buscar Model

            mainCam = cameraOverride
                      ?? GetComponentInChildren<UnityEngine.Camera>() 
                      ?? GetComponentInParent<UnityEngine.Camera>();

            if (!rightStasisOrigin && stasisOriginLegacy) rightStasisOrigin = stasisOriginLegacy;
            if (_view) OnShoot += _view.OnShootEvent;

            // Suscribir a eventos del Model para saber el lado de pared
            if (_model)
            {
                _model.OnWallrunStart += HandleWallrunStart;  // dir < 0 => pared izq, dir > 0 => pared der
                _model.OnWallrunEnd   += HandleWallrunEnd;
            }
            else if (debugLogs)
            {
                Debug.LogWarning("[StasisGun] No se encontró Model en padres; no se usará preferencia de mano por wallrun.");
            }

            if (!mainCam && debugLogs)
                Debug.LogWarning("[StasisGun] No se encontró cámara. Asigna una Camera en escena.");


            GameManager.Instance.OnDeathPlayer += PlayerDeath;
        }
        private void PlayerDeath()
        {
            foreach (var item in _stasisList)
            {
                item.stasis.StatisEffectDeactivate();
               
            }
            _stasisList.Clear();
        }
        private void OnDestroy()
        {
            if (_model)
            {
                _model.OnWallrunStart -= HandleWallrunStart;
                _model.OnWallrunEnd   -= HandleWallrunEnd;
            }
        }

        private void Update()
        {
            if (!canShootStasis) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // 1) Releasing target directo
            PhysicsBox releasing = null;
            if (_playerInteractor)
            {
                releasing = _playerInteractor.GetReleasingTarget();
                if (releasing && !releasing.gameObject.activeInHierarchy) releasing = null;
            }

            if (releasing)
            {
                var stasisComp = (IStasis)releasing;
                var col = releasing.GetComponentInChildren<Collider>();
                var hitPoint = col ? col.bounds.center : releasing.transform.position;
                FireDirect(releasing.gameObject, stasisComp, hitPoint);
                return;
            }

            // 2) Flujo normal con raycast real
            TryApplyStasis();
        }

        public void ActivateGun() => canShootStasis = true;
        public void DeactivateGun() => canShootStasis = false;
        public void ActivateStasis()
        {
            _stasisActivate = true;
        }
       
        
        private void TryApplyStasis()
        
        {
            if (!_stasisActivate) return;
            if (!canShootStasis) return;
            if (!mainCam)
            {
                if (debugLogs) Debug.LogWarning("[StasisGun] TryApplyStasis abortado: no hay Camera asignada.");
                return;
            }

            int mask = layer.value != 0 ? layer.value : ~0;
            if (layer.value == 0 && debugLogs)
                Debug.LogWarning("[StasisGun] LayerMask objetivo está en 0; usando fallback ~0 temporalmente.");

            canShootStasis = false;
            StartCoroutine(ResetShootAfter(cooldown));
            OnShoot?.Invoke();

            Ray ray = GetForwardRay(camHolder, 20);

            bool gotHit = Physics.SphereCast(ray, radiusStasis, out RaycastHit hit, maxDistance, mask,
                                 QueryTriggerInteraction.Ignore)
                          || Physics.Raycast(ray, out hit, maxDistance, mask, QueryTriggerInteraction.Ignore);

            bool stasisHit = false;
            Vector3 targetPoint = gotHit
                                  ? hit.point
                                  : ray.origin + ray.direction * Mathf.Min(25f, maxDistance * 0.2f);

            if (gotHit)
            {
                // >>> NUEVO BLOQUE: resolver IStasis desde proxy o desde padres <<<
                IStasis stasisComponent = null;
                GameObject objStaseable = null;

                // 1) Si le pegamos a un proxy registrado
                if (StasisRegistry.TryGet(hit.collider, out var stasisFromProxy))
                {
                    stasisComponent = stasisFromProxy;
                    objStaseable = ((MonoBehaviour)stasisComponent).gameObject;
                }
                else
                {
                    // 2) Buscar en la jerarquía del objeto impactado (plataforma hija → engrane padre)
                    var stasisInParents = hit.collider.GetComponentInParent<IStasis>();
                    if (stasisInParents != null)
                    {
                        stasisComponent = stasisInParents;
                        objStaseable = ((MonoBehaviour)stasisComponent).gameObject;
                    }
                }

                if (stasisComponent != null)
                {
                    // 3) Mantener tu lógica de StasisRoot (si existe, preferir el IStasis dentro del root)
                    var root = objStaseable.GetComponentInParent<StasisRoot>();
                    if (root)
                    {
                        var found = root.GetComponentsInChildren<MonoBehaviour>()
                                        .OfType<IStasis>()
                                        .FirstOrDefault();
                        if (found != null)
                        {
                            stasisComponent = found;
                            objStaseable = ((MonoBehaviour)stasisComponent).gameObject;
                        }
                    }

                    stasisHit = true;

                    // EXACTAMENTE como ya hacías: delay corto y toggle en ApplyStasisEffect
                    StartCoroutine(WaitStasisEffect(objStaseable, stasisComponent));
                }
                else
                {
                    // No encontramos IStasis ni en proxy ni en padres → es un miss
                    SpawnMissFx(targetPoint, hit.normal);
                }
            }
            else
            {
                SpawnMissFx(targetPoint, -ray.direction);
            }

            // Elegir mano(s) según wallrun/spam
            var fireMode = ChooseHandForThisShot();

            // Spawnear beam desde el(los) brazo(s) seleccionados (siempre hay FX)
            SpawnBeamsNextFrame(targetPoint, stasisHit, fireMode);

            // Debug
            DrawDebugShot(ray, gotHit, hit, stasisHit);
        }

        private Ray GetForwardRay(Transform origin, float distance)
        {
            if (!origin)
            {
                Debug.LogWarning("[StasisGun] No se asignó un Transform válido para GetForwardRay.");
                return new Ray(Vector3.zero, Vector3.forward);
            }

            // El rayo empieza en la posición del Transform
            // y va en la dirección de su forward
            return new Ray(origin.position, origin.forward);
        }

        private void FireDirect(GameObject targetObj, IStasis stasisComp, Vector3 hitPoint)
        {
            if (!canShootStasis) return;

            canShootStasis = false;
            StartCoroutine(ResetShootAfter(cooldown));
            OnShoot?.Invoke();

            // Delay corto y toggle en ApplyStasisEffect (mismo patrón que funcionaba)
            StartCoroutine(WaitStasisEffect(targetObj, stasisComp));

            var fireMode = ChooseHandForThisShot();
            SpawnBeamsNextFrame(hitPoint, true, fireMode);

            if (_playerInteractor)
                _playerInteractor.ClearReleasingTargetIf(targetObj);

            if (debugLogs) Debug.Log($"[StasisGun] Direct STASIS to releasing target: {targetObj.name}", targetObj);
        }

        private enum FireMode { Right, Left, Alternate /*reservado si quisieras doble simultáneo*/ }

        private FireMode ChooseHandForThisShot()
        {
            // 1) Si estamos en wallrun, usar SIEMPRE el brazo opuesto a la pared (evento del Model)
            if (_isWallrunning)
            {
                var use = (_wallOnSide == Hand.Left) ? Hand.Right : Hand.Left;
                _lastHandUsed = use;
                _lastShotTime = Time.time;
                return use == Hand.Right ? FireMode.Right : FireMode.Left;
            }

            // 2) Spam: alternar si el click llegó dentro de la ventana
            float dt = Time.time - _lastShotTime;
            if (dt <= spamWindow)
            {
                _lastHandUsed = (_lastHandUsed == Hand.Right) ? Hand.Left : Hand.Right;
                _lastShotTime = Time.time;
                return _lastHandUsed == Hand.Right ? FireMode.Right : FireMode.Left;
            }

            // 3) Normal: siempre derecho
            _lastHandUsed = Hand.Right;
            _lastShotTime = Time.time;
            return FireMode.Right;
        }

        // Eventos del Model
        private void HandleWallrunStart(int dir)
        {
            _isWallrunning = true;
            _wallOnSide = (dir < 0f) ? Hand.Left : Hand.Right;
            if (debugLogs)
                Debug.Log($"[StasisGun] WallrunStart. Pared: {(_wallOnSide == Hand.Left ? "Izquierda" : "Derecha")} → disparo con {( _wallOnSide == Hand.Left ? "Derecha" : "Izquierda")}");
        }

        private void HandleWallrunEnd()
        {
            _isWallrunning = false;
            if (debugLogs) Debug.Log("[StasisGun] WallrunEnd.");
        }

        private void SpawnBeamsNextFrame(Vector3 hitPoint, bool stasisHit, FireMode mode)
        {
            StartCoroutine(SpawnBeamsCR(hitPoint, stasisHit, mode));
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator SpawnBeamsCR(Vector3 hitPoint, bool stasisHit, FireMode mode)
        {
            yield return null;

            Transform right = rightStasisOrigin;
            Transform left  = leftStasisOrigin;

            // Fallbacks
            if (!right && stasisOriginLegacy) right = stasisOriginLegacy;
            if (!right && !left)
            {
                if (debugLogs) Debug.LogWarning("[StasisGun] No hay orígenes de FX (right/left). No se spawnea beam.");
                yield break;
            }

            // Si tenías un solo beam persistente, limpiamos el anterior
            if (_activeBeam) Destroy(_activeBeam.gameObject);

            switch (mode)
            {
                case FireMode.Right:
                    SpawnOneBeam((right ? right.position : left.position), hitPoint, stasisHit);
                    break;
                case FireMode.Left:
                    SpawnOneBeam((left ? left.position : right.position), hitPoint, stasisHit);
                    break;
                case FireMode.Alternate:
                    // Guardado por si alguna vez querés disparar ambos simultáneo
                    SpawnOneBeam((right ? right.position : left.position), hitPoint, stasisHit);
                    SpawnOneBeam((left ? left.position : right.position),  hitPoint, stasisHit);
                    break;
            }
            
        }

        private void SpawnOneBeam(Vector3 origin, Vector3 hitPoint, bool stasisHit)
        {
            if (!stasisBeamPrefab) return;
            var go = Instantiate(stasisBeamPrefab, origin, Quaternion.identity);
            var beam = go.GetComponent<StasisBeam>();
            if (beam) beam.SetBeam(origin, hitPoint, stasisHit);
            _activeBeam = beam;
        }

        private IEnumerator ResetShootAfter(float t)
        {
            yield return new WaitForSeconds(t);
            canShootStasis = true;
        }

        private IEnumerator WaitStasisEffect(GameObject hitObject, IStasis stasisComponent)
        {
            // pequeño delay para sincronizar con FX (idéntico a tu script que andaba)
            yield return new WaitForSeconds(0.06f);
            ApplyStasisEffect(hitObject, stasisComponent);
        }

        private void ApplyStasisEffect(GameObject newObject, IStasis newStasisComponent)
        {
            // MISMA lógica de tu StasisGun2 (toggle por GameObject)
            int idx = _stasisList.FindIndex(x => x.obj == newObject);
            if (idx != -1)
            {
                _stasisList[idx].stasis.StatisEffectDeactivate();
                _stasisList.RemoveAt(idx);
                return;
            }

            if (_stasisList.Count >= _maxStasisObjects)
            {
                _stasisList[0].stasis.StatisEffectDeactivate();
                _stasisList.RemoveAt(0);
            }

            _stasisList.Add((newObject, newStasisComponent));
            newStasisComponent.StatisEffectActivate();
        }

        private void SpawnMissFx(Vector3 point, Vector3 normal)
        {
            if (!particleStasisMissed) return;
            Vector3 spawnPos = point + normal.normalized * 0.15f;
            GameObject fx = Instantiate(particleStasisMissed, spawnPos, Quaternion.LookRotation(normal));
            fx.SetActive(true);
            var ps = fx.GetComponent<ParticleSystem>();
            if (ps) ps.Play();
            Destroy(fx, 5f);
        }

        private void OnDisable()
        {
            foreach (var (obj, st) in _stasisList) st.StatisEffectDeactivate();
            _stasisList.Clear();
        }

        private void DrawDebugShot(Ray ray, bool gotHit, RaycastHit hit, bool stasisHit)
        {
            if (!debugDraw) return;

            if (gotHit)
            {
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, stasisHit ? Color.green : debugRayColor, debugPersist);
                if (rightStasisOrigin)
                    Debug.DrawLine(rightStasisOrigin.position, hit.point, debugBeamColor, debugPersist);
                if (leftStasisOrigin)
                    Debug.DrawLine(leftStasisOrigin.position, hit.point, debugBeamColor, debugPersist);
                Debug.DrawRay(hit.point, hit.normal * 0.5f, debugNormalColor, debugPersist);
            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction * Mathf.Min(50f, maxDistance), debugMissColor, debugPersist);
            }
        }
    }
}