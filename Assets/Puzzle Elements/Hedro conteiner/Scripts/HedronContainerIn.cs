using System;
using System.Collections;
using Managers.Game;
using Player.Scripts.Interactor;
using Puzzle_Elements.Hedron.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace Puzzle_Elements.Hedro_conteiner.Scripts
{
    [RequireComponent(typeof(Collider))]
    public class HedronContainerIn : MonoBehaviour, IInteractable
    {
        [SerializeField] private PhysicsBox _box;

        [Header("=== CORE REFERENCES ===")]
        [SerializeField] private Button.Scripts.Button _button;
        public Transform anchor;
        [SerializeField] private Animator _anim;
        [SerializeField] private Transform pos; // centro de atracción / eyección

        [Header("=== ANIMATION CONFIGURATION ===")]
        [SerializeField] private string openTrigger = "OPEN";
        [SerializeField] private string closeTrigger = "Close";

        [Header("=== TIMING SETTINGS ===")]
        [SerializeField] private float posDelaySeconds = 1.5f;
        [SerializeField] private float openToEjectDelay = 2f;
        [SerializeField] private float ejectCooldown = 0.15f;
        [SerializeField] private float scaleUpSeconds = 0.35f;
        [SerializeField] private float shrinkDuration = 1.2f; // sin usar, pero lo dejo
        [SerializeField] private float panelShowDelay = 2f;
        [SerializeField] private float panelCloseDelay = 1f;

        [Header("=== MOVEMENT & PHYSICS ===")]
        [SerializeField] private float attractionSpeed = 5f; // si no usás curvas
        [SerializeField] private float stopDistance = 0.05f;
        [SerializeField] private float ejectNudgeDistance = 0.08f;
        [SerializeField] private float ejectTorque;
        [SerializeField] private bool reenableColliderOnEject = true;

        [Header("=== SCALING SETTINGS ===")]
        [SerializeField] private Vector3 targetScale = new Vector3(0.5f, 0.5f, 0.5f);

        [Header("=== ATTRACTION SMOOTHING ===")]
        [SerializeField] private bool useSmoothAttract = true;
        [SerializeField] private float attractMaxSeconds = 1.2f;
        [SerializeField] private AnimationCurve posCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve rotCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Dock cuando la escala actual esté dentro de este margen respecto a targetScale (ej. 1.05 = ±5%).")]
        [SerializeField] private float dockScaleThreshold = 1.05f;
        [SerializeField] private bool alignRotationOnDock = true;

        [Header("=== VISUAL EFFECTS ===")]
        [SerializeField] private float brightnessFrequency = 2f;
        [SerializeField] private float brightnessAmplitude = 0.05f;
        [SerializeField] private Color glowColor = Color.white;
        public Renderer rightDoorRenderer;
        public Renderer leftDoorRenderer;

        [Header("=== UI CONFIGURATION ===")]
        public GameObject panel;
        public TextMeshProUGUI E;
        public TextMeshProUGUI message;
        [SerializeField] private string extractionMessage = "¿Deseas extraer el hedro?";
        [SerializeField] private string buttonExtractionText = "¿Deseas extraer el Hedron?";

        [Header("=== FILTER SETTINGS ===")]
        public LayerMask acceptMask = ~0;

        [Header("=== EVENTS ===")]
        public UnityEvent onHedronPlaced;
        public UnityEvent onHedronRemoved;

        // Private variables
        private Material _rightMat, _leftMat;
        private Color _baseEmissionRight, _baseEmissionLeft;
        private Transform _current;
        private Rigidbody _rb;
        private bool _attracting;
        private bool _placedFired;
        private bool _isEjecting;
        private float _lastEjectTime;

        // Public properties
        public bool HasOccupant => _current != null;
        public bool IsAttracting => _attracting;
        public bool IsEjecting => _isEjecting;
        public PhysicsBox CurrentBox => _box;
        public Transform CurrentHedron => _current;
        public Vector3 TargetScale { get => targetScale; set => targetScale = value; }
        public float PanelShowDelay { get => panelShowDelay; set => panelShowDelay = value; }

        private bool canAtracction = true;
        private bool canOpenPanel = true;

        public Action OnHedroContainerActivate;

        // ====== STATE MACHINE ======
        private enum ContainerState
        {
            Idle,
            Attracting,
            Docked,
            Opening,
            Ejecting
        }

        [SerializeField] private ContainerState _state = ContainerState.Idle;

        // ====== Timers / datos de estados ======

        // Atracción
        private float _attractTimer;
        private float _attractDuration;
        private Vector3 _attractStartPos;
        private Quaternion _attractStartRot;
        private Vector3 _attractStartScale;
        private Vector3 _attractEndPos;
        private Quaternion _attractEndRot;
        private Vector3 _attractEndScale;

        // Panel
        private float _panelShowTimer;
        private float _panelCloseTimer;
        private float _panelBlockTimer;

        // Bloqueo de atracción tras eyección
        private float _cantAtracctionTimer;

        // Opening -> espera antes de eyectar
        private float _openingTimer;

        // Ejecting
        public float _ejectMoveSpeed = 6f;
        private Vector3 _ejectTargetPos;
        public float _ejectScaleTimer;

        // Glow
        private bool _glowActive;
        private float _glowTimer;
        private float _glowDuration;

        void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;

            if (rightDoorRenderer != null)
            {
                _rightMat = Instantiate(rightDoorRenderer.sharedMaterial);
                rightDoorRenderer.material = _rightMat;
                _baseEmissionRight = _rightMat.GetColor("_EmissionColor");
            }
            if (leftDoorRenderer != null)
            {
                _leftMat = Instantiate(leftDoorRenderer.sharedMaterial);
                leftDoorRenderer.material = _leftMat;
                _baseEmissionLeft = _leftMat.GetColor("_EmissionColor");
            }
        }

        void Start()
        {
            if (_button != null) _button.SetText(_button.E, "");
            if (panel != null) panel.gameObject.SetActive(false);
            if (message != null) message.gameObject.SetActive(false);
            if (E != null) E.gameObject.SetActive(false);
        }

        void Update()
        {
            // Timers globales
            UpdateBlockAttractionTimer();
            UpdatePanelBlockTimer();
            UpdatePanelTimers();
            UpdateGlow();

            // Máquina de estados principal
            switch (_state)
            {
                case ContainerState.Idle:
                    UpdateIdle();
                    break;

                case ContainerState.Attracting:
                    UpdateAttracting();
                    break;

                case ContainerState.Docked:
                    UpdateDocked();
                    break;

                case ContainerState.Opening:
                    UpdateOpening();
                    break;

                case ContainerState.Ejecting:
                    UpdateEjecting();
                    break;
            }
        }

        // ================== STATE UPDATES ==================

        void UpdateIdle()
        {
            _attracting = false;
            _isEjecting = false;
        }

        void UpdateAttracting()
        {
            if (_current == null || _box == null || pos == null)
            {
                ResetToIdle();
                return;
            }

            if (_rb == null)
                _rb = _current.GetComponent<Rigidbody>();

            // Si algo cambió drásticamente (player lo agarró, rigidbody dejó de ser kinematic, etc.)
            if (_rb != null && (!_rb.isKinematic || _rb.transform != _current))
            {
                ResetToIdle();
                return;
            }

            _attracting = true;

            _attractTimer += Time.deltaTime;
            float t01 = Mathf.Clamp01(_attractTimer / _attractDuration);

            float p = useSmoothAttract ? posCurve.Evaluate(t01) : t01;
            float r = useSmoothAttract ? rotCurve.Evaluate(t01) : t01;
            float s = useSmoothAttract ? scaleCurve.Evaluate(t01) : t01;

            // Movimiento hacia el centro
            _current.position = Vector3.LerpUnclamped(_attractStartPos, _attractEndPos, p);

            // Rotación “show”
            Quaternion baseRot = Quaternion.SlerpUnclamped(_attractStartRot, _attractEndRot, r);
            _current.rotation = baseRot;
            _current.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);

            // Escala hacia targetScale
            _current.localScale = Vector3.LerpUnclamped(_attractStartScale, _attractEndScale, s);

            bool closeEnough = Vector3.Distance(_current.position, _attractEndPos) <= stopDistance;

            bool smallEnough =
                Mathf.Abs(_current.localScale.x - _attractEndScale.x) <= _attractEndScale.x * (dockScaleThreshold - 1f) &&
                Mathf.Abs(_current.localScale.y - _attractEndScale.y) <= _attractEndScale.y * (dockScaleThreshold - 1f) &&
                Mathf.Abs(_current.localScale.z - _attractEndScale.z) <= _attractEndScale.z * (dockScaleThreshold - 1f);

            if (t01 >= 1f || (closeEnough && smallEnough))
            {
                DockObject();
            }
        }

        void UpdateDocked()
        {
            _attracting = false;
            // Espera Interact()
        }

        void UpdateOpening()
        {
            if (!HasOccupant || _box == null)
            {
                ResetToIdle();
                return;
            }

            _openingTimer += Time.deltaTime;
            _box.transform.position = pos.transform.position;
            ShowPhysicsBox();

            // Cuando termina la anim de apertura (aprox), pasamos a ejecting
            if (_openingTimer >= openToEjectDelay)
            {
                _box.transform.position = pos.transform.position;
                BeginEjectFromCenter();
            }
        }

        public void TeleportPhysicsBox()
        {
            if (_box != null)
            {
                if (Vector3.Distance(transform.position, _box.transform.position) > 5)
                {
                    //_box.transform.position = transform.position + new Vector3(0, 4, 6);
                    Rigidbody rb = _box.GetComponent<Rigidbody>();
                    rb.useGravity = false;
                }
            }
        }

        void UpdateEjecting()
        {
            if (_box == null)
            {
                ResetToIdle();
                return;
            }

            Transform t = _box.transform;
            EnableAllColliders(t, false);

            // Si el player lo agarró y lo movió de jerarquía, salimos limpio
            if (!t.gameObject.activeInHierarchy)
            {
                ResetToIdle();
                return;
            }

            // Movimiento hacia fuera
            t.position = Vector3.MoveTowards(t.position, _ejectTargetPos, _ejectMoveSpeed * Time.deltaTime);

            // Rotación “show” en sentido contrario
            t.Rotate(Vector3.up, -180f * Time.deltaTime, Space.World);

            // Escala de targetScale -> Vector3.one
            if (scaleUpSeconds > 0.01f)
            {
                _ejectScaleTimer += Time.deltaTime;
                float st = Mathf.Clamp01(_ejectScaleTimer / scaleUpSeconds);
                Vector3 startScale = targetScale;
                Vector3 endScale = Vector3.one;
                t.localScale = Vector3.LerpUnclamped(startScale, endScale, st);
            }
            else
            {
                t.localScale = Vector3.one;
            }

            // Llegó al final
            if (Vector3.Distance(t.position, _ejectTargetPos) <= 0.01f)
            {
                FinishEject(t);
            }
        }

        // ================== ATRACCIÓN / DOCK ==================

        void DockObject()
        {
            if (_current == null || pos == null) { ResetToIdle(); return; }

            // Encaje final
            _current.position = pos.position;
            if (alignRotationOnDock)
                _current.rotation = pos.rotation;
            _current.localScale = targetScale;

            if (_rb == null)
                _rb = _current.GetComponent<Rigidbody>();

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.useGravity = false;
                _rb.isKinematic = true;
            }

            // Desactivar collider principal del hedro (si está en el root)
            var mainCol = _current.GetComponent<BoxCollider>();
            if (mainCol != null) mainCol.enabled = false;

            if (anchor != null)
                _current.SetParent(anchor, true);
            else
                _current.SetParent(transform, true);

            if (!_placedFired)
            {
                _placedFired = true;

                onHedronPlaced?.Invoke();
                OnHedroContainerActivate?.Invoke();

                var rbBox = _box != null ? _box.GetComponent<Rigidbody>() : null;
                if (rbBox != null) rbBox.useGravity = false;

                if (_box != null)
                {
                    _box.transform.SetParent(transform, true);
                    // _box.gameObject.SetActive(false); // caja “guardada”
                }

                if (_anim != null && !string.IsNullOrEmpty(closeTrigger))
                    _anim.SetTrigger(closeTrigger);

                if (_button != null)
                    _button.SetText(_button.E, buttonExtractionText);

                StartCoroutine(waitDontShowBox());
                StartGlowOnce();
                ActivatePanel();
            }

            _attracting = false;
            _state = ContainerState.Docked;
        }

        IEnumerator waitDontShowBox()
        {
            yield return new WaitForSeconds(1.5f);
            DontShowPhysicsBox();
        }

        // ================== TRIGGERS ==================
        public void DontShowPhysicsBox()
        {
            if (_box != null)
            {
                _box.gameObject.SetActive(false);
            }
        }

        public void ShowPhysicsBox()
        {
            if (_box != null)
            {
                _box.gameObject.SetActive(true);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (_current != null) return;
            if (!enabled) return;
            if (!canAtracction) return;
            if (pos == null) return;

            // Filtro por máscara de capas
            if (((1 << other.gameObject.layer) & acceptMask) == 0) return;

            // Buscamos un PhysicsBox en el collider o en sus padres
            if (!HasPhysicsBox(other.gameObject)) return;

            // Si el player tiene algo agarrado, no tocamos nada
            var interactor = GameManager.Instance.player.GetComponentInChildren<PlayerInteractor>();
            if (interactor != null && interactor._objectGrabbable != null)
                return;

            // Atraemos SIEMPRE el root del PhysicsBox
            _current = _box.transform;
            _rb = _box.GetComponent<Rigidbody>();

            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.useGravity = false;
                _rb.isKinematic = true;
            }

            StartAttraction();
        }

        //void OnTriggerExit(Collider other)
        //{
        //    // Si estamos atrayendo este mismo PhysicsBox y sale del trigger, reseteamos
        //    var box = other.GetComponentInParent<PhysicsBox>();
        //    if (box != null && box == _box && _state == ContainerState.Attracting)
        //    {
        //        ResetToIdle();
        //    }
        //}

        void StartAttraction()
        {
            Debug.Log("Atrayendo");

            if (_current == null || pos == null)
            {
                ResetToIdle();
                return;
            }

            _attractStartPos = _current.position;
            _attractStartRot = _current.rotation;
            _attractStartScale = _current.localScale;

            _attractEndPos = pos.position;
            _attractEndRot = pos.rotation;
            _attractEndScale = targetScale;

            _attractDuration = useSmoothAttract
                ? Mathf.Max(0.05f, attractMaxSeconds)
                : Mathf.Max(0.05f, Vector3.Distance(_attractStartPos, _attractEndPos) / Mathf.Max(0.01f, attractionSpeed));

            _attractTimer = 0f;

            // Deshabilitar MeshCollider del PhysicsBox si existe (en root)
            MeshCollider mc = _box != null ? _box.GetComponent<MeshCollider>() : null;
            if (mc != null)
            {
                mc.enabled = false;
            }

            _attracting = true;
            _state = ContainerState.Attracting;
        }

        // ================== EYECCIÓN ==================

        public void OpenAndEject()
        {
            if (_isEjecting) return;
            if (!HasOccupant || _box == null) return;
            if (_state != ContainerState.Docked) return;
            if (Time.time - _lastEjectTime < ejectCooldown) return;

            canAtracction = false;
            _cantAtracctionTimer = 3f;

            _isEjecting = true;
            _lastEjectTime = Time.time;

            if (_anim != null && !string.IsNullOrEmpty(openTrigger))
                _anim.SetTrigger(openTrigger);

            _openingTimer = 0f;
            _state = ContainerState.Opening;
        }

        public void EjectNow()
        {
            if (_isEjecting) return;
            if (!HasOccupant || _box == null) return;

            canAtracction = false;
            _cantAtracctionTimer = 3f;

            _isEjecting = true;
            _lastEjectTime = Time.time;

            if (_anim != null && !string.IsNullOrEmpty(openTrigger))
                _anim.SetTrigger(openTrigger);

            _openingTimer = openToEjectDelay; // para forzar que pase directo
            _state = ContainerState.Opening;
        }

        public void ForceEject() => EjectNow();

        public void ClearContainer()
        {
            ResetToIdle();
        }

        public bool CanAcceptObject(GameObject obj)
        {
            if (_current != null || !enabled || pos == null) return false;
            if (((1 << obj.layer) & acceptMask) == 0) return false;
            return HasPhysicsBox(obj);
        }

        void BeginEjectFromCenter()
        {
            if (_box == null)
            {
                ResetToIdle();
                return;
            }

            // Activar caja y sacarla del contenedor
            _box.gameObject.SetActive(true);
            _box.enabled = false; // componente PhysicsBox desactivado durante el launch
            _box.transform.SetParent(null, true);

            // Opcional: desparentar el hedro interno
            if (_current != null)
                _current.SetParent(null, true);

            if (_rb == null)
                _rb = _box.GetComponent<Rigidbody>();

            if (_rb != null)
            {
                _rb.isKinematic = true;
                _rb.useGravity = false;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }

            // Pos inicial de eyección: centro POS
            if (pos != null)
            {
                _box.transform.position = pos.position;
                _box.transform.rotation = pos.rotation;
            }
            else if (anchor != null)
            {
                _box.transform.position = anchor.position;
                _box.transform.rotation = anchor.rotation;
            }

            // Destino: hacia adelante 1 unidad
            _ejectTargetPos = pos.transform.position + pos.transform.forward * 1.4f;

            // Escala inicial: igual targetScale
            _box.transform.localScale = targetScale;
            _ejectScaleTimer = 0f;

            _state = ContainerState.Ejecting;
        }

        void FinishEject(Transform t)
        {
            if (t == null)
            {
                ResetToIdle();
                return;
            }

            MeshCollider mc = _box != null ? _box.GetComponent<MeshCollider>() : null;
            if (mc != null)
            {
                mc.enabled = true;
            }

            if (reenableColliderOnEject)
            {
                EnableAllColliders(t, true);
            }

            if (_rb == null)
                _rb = t.GetComponent<Rigidbody>();

            if (_rb != null)
            {
                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.WakeUp();

                if (ejectTorque > 0f)
                {
                    Vector3 rand = Random.onUnitSphere * ejectTorque;
                    _rb.AddTorque(rand, ForceMode.VelocityChange);
                }
            }

            TeleportPhysicsBox();
            onHedronRemoved?.Invoke();
            OnHedroContainerActivate?.Invoke();

            _isEjecting = false;
            ResetToIdle();
        }

        IEnumerator waitShowBox()
        {
            yield return new WaitForSeconds(1.5f);
            ShowPhysicsBox();
        }

        // ================== TIMERS, GLOW, UTILIDADES ==================

        void ResetToIdle()
        {
            _current = null;
            _rb = null;
            _attracting = false;
            _placedFired = false;
            _isEjecting = false;
            _box = null;
            _openingTimer = 0f;
            _attractTimer = 0f;

            if (_button != null)
                _button.SetText(_button.E, "");

            _state = ContainerState.Idle;
        }

        bool HasPhysicsBox(GameObject go)
        {
            // Soporta colliders en hijos: buscamos en padres
            PhysicsBox box = go.GetComponentInParent<PhysicsBox>();
            if (box != null)
            {
                _box = box;
                return true;
            }

            _box = null;
            return false;
        }

        void EnableAllColliders(Transform root, bool enabled)
        {
            var cols = root.GetComponentsInChildren<MeshCollider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = enabled;
        }

        void UpdateBlockAttractionTimer()
        {
            if (_cantAtracctionTimer > 0f)
            {
                _cantAtracctionTimer -= Time.deltaTime;
                if (_cantAtracctionTimer <= 0f)
                {
                    canAtracction = true;
                }
            }
        }

        void UpdatePanelBlockTimer()
        {
            if (_panelBlockTimer > 0f)
            {
                _panelBlockTimer -= Time.deltaTime;
                if (_panelBlockTimer <= 0f)
                {
                    canOpenPanel = true;
                }
            }

            if (!canOpenPanel && panel != null && panel.activeSelf)
                panel.SetActive(false);
        }

        void UpdatePanelTimers()
        {
            if (_panelShowTimer > 0f)
            {
                _panelShowTimer -= Time.deltaTime;
                if (_panelShowTimer <= 0f)
                {
                    if (panel != null && canOpenPanel)
                        panel.SetActive(true);
                    if (E != null)
                    {
                        E.gameObject.SetActive(true);
                        E.text = extractionMessage;
                    }
                }
            }

            if (_panelCloseTimer > 0f)
            {
                _panelCloseTimer -= Time.deltaTime;
                if (_panelCloseTimer <= 0f)
                {
                    if (message != null) message.gameObject.SetActive(false);
                    if (panel != null) panel.gameObject.SetActive(false);
                }
            }
        }

        void UpdateGlow()
        {
            if (!_glowActive) return;

            float f = Mathf.Max(0.0001f, brightnessFrequency);
            _glowTimer += Time.deltaTime;

            float phase = _glowTimer * f;
            float brightness = (Mathf.Cos(phase) + 1f) * 0.5f;

            if (_rightMat != null)
            {
                Color e = _baseEmissionRight + (glowColor * brightness * brightnessAmplitude);
                _rightMat.SetColor("_EmissionColor", e);
            }
            if (_leftMat != null)
            {
                Color e = _baseEmissionLeft + (glowColor * brightness * brightnessAmplitude);
                _leftMat.SetColor("_EmissionColor", e);
            }

            if (_glowTimer >= _glowDuration)
            {
                if (_rightMat != null)
                {
                    _rightMat.SetColor("_EmissionColor", _baseEmissionRight);
                    _rightMat.DisableKeyword("_EMISSION");
                }
                if (_leftMat != null)
                {
                    _leftMat.SetColor("_EmissionColor", _baseEmissionLeft);
                    _leftMat.DisableKeyword("_EMISSION");
                }

                _glowActive = false;
            }
        }

        void StartGlowOnce()
        {
            if (_rightMat != null) _rightMat.EnableKeyword("_EMISSION");
            if (_leftMat != null) _leftMat.EnableKeyword("_EMISSION");

            float f = Mathf.Max(0.0001f, brightnessFrequency);
            _glowDuration = Mathf.PI / f;
            _glowTimer = 0f;
            _glowActive = true;
        }

        private void ActivatePanel()
        {
            _panelShowTimer = panelShowDelay;
        }

        public void ClosePanelByTren()
        {
            canOpenPanel = false;
            _panelBlockTimer = 5f;
            if (panel != null) panel.SetActive(false);
        }

        public void Interact()
        {
            if (panel != null && panel.gameObject.activeSelf)
            {
                if (E != null) E.gameObject.SetActive(false);
                if (message != null) message.gameObject.SetActive(true);

                OpenAndEject();

                _panelCloseTimer = panelCloseDelay;
            }
        }
    }
}
