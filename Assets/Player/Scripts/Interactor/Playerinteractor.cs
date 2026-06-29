using System;
using System.Collections;
using Managers.Events;
using Managers.Game;
using Player.Scripts.MovementFSM.MVC;
using Player.Stasis;
using Puzzle_Elements.Hedron.Scripts;
using UIScripts;
using UnityEngine;

namespace Player.Scripts.Interactor
{
    public class PlayerInteractor : MonoBehaviour
    {
        public event Action OnInteractableFocusEnter = delegate { };
        public event Action OnInteractableFocusExit = delegate { };
        public event Action OnInteractPerformed = delegate { };

        [Header("Interaction Settings")] [SerializeField]
        private float throwCharge;

        [SerializeField] private float pickUpRange = 4f;
        [SerializeField] private float holdTime;
        [SerializeField] private float throwHoldThreshold = 0.15f;
        public float ThrowCharge => Mathf.Clamp01(throwCharge / throwHoldThreshold);
        private bool _isHoldingThrow;

        [SerializeField] private float throwForce = 10f;
        [SerializeField] private Transform objectGrabPointTransform;
        [SerializeField] private Transform objectGrabPointBackTransform;

        [Header("Release Target")] [SerializeField]
        private float releaseAimWindow = 0.20f;

        private PhysicsBox _releasingTarget;
        private float _releaseAimUntil = -999f;

        
        public PhysicsBox GetReleasingTarget()
        {
            return (Time.time <= _releaseAimUntil) ? _releasingTarget : null;
        }

        private void MarkReleasing(PhysicsBox pb)
        {
            _releasingTarget = pb;
            _releaseAimUntil = Time.time + releaseAimWindow;
        }

        public PhysicsBox _objectGrabbable;

        [Header("Environment")]
        [Tooltip("Capas de entorno sólidas. Los interactuables serán ignorados en LOS.")]
        [SerializeField]
        private LayerMask environmentMask = ~0;

        [Tooltip("Capas de objetos agarrables (PhysicsBox).")] [SerializeField]
        private LayerMask grabbableMask;

        [Header("Smoothing")] [SerializeField] private float rotationSmoothSpeed = 10f;

        [Header("FX Settings")] [SerializeField]
        private StasisObjectEffects stasisEffects;

        [SerializeField] private StasisGun stasisGun;

        private Quaternion _rotationSmoothQuat;
        private View _view;

        public event Action OnGrabItem = delegate { };

        [Header("Interactable Focus")] [SerializeField]
        private float focusSphereRadius = 0.15f;

        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private float enterDistance = 4f;
        [SerializeField] private float exitDistance = 4.5f;
        [Range(1f, 30f)] [SerializeField] private float enterAngleDeg = 6f;
        [Range(1f, 45f)] [SerializeField] private float exitAngleDeg = 8f;
        [SerializeField] private float enterStableTime = 0.06f;
        [SerializeField] private float exitStableTime = 0.08f;
        [SerializeField] private float castStartOffset = 0.05f;

        [Header("Debug")] [SerializeField] private bool debugFocus;

        private GameObject _currentFocused;
        private float _lastFocusHitDistance = Mathf.Infinity;

        private GameObject _pendingEnterTarget;
        private float _pendingEnterSince = -999f;
        private float _lostSince = -999f;

        [Header("Hand Hold")] [Tooltip("Palma izquierda (UP = normal de la palma).")] [SerializeField]
        private Transform leftPalmTransform;

        [Tooltip("Palma derecha (UP = normal de la palma).")] [SerializeField]
        private Transform rightPalmTransform;

        private Transform _currentPalm; // interna (no aparece en el inspector)

        [Tooltip("Altura sobre la palma a lo largo del UP de la mano")] [SerializeField]
        private float palmHeight = 0.12f;

        [Tooltip("Velocidad del giro 'mágico' en grados/seg")] [SerializeField]
        private float hoverSpinSpeed = 50f;

        [Tooltip("Tiempo de aproximación y escalado al agarrar")] [SerializeField]
        private float pickupApproachTime = 0.22f;

        [Tooltip("Tiempo de cambio de palma durante wallrun")] [SerializeField]
        private float handSwitchTime = 0.18f;

        private Model _model;

        void Start()
        {
            _rotationSmoothQuat = objectGrabPointTransform ? objectGrabPointTransform.rotation : Quaternion.identity;
            _view = GetComponentInParent<View>();
            OnGrabItem += _view.OnGrabEvent;
            if (GameManager.Instance)
            {
                GameManager.Instance.OnDeathPlayer += TryDropOnDeath;
            }

            _currentPalm = rightPalmTransform ?? leftPalmTransform ?? objectGrabPointBackTransform;
            if (!_currentPalm)
                Debug.LogWarning("[PlayerInteractor] No hay palms asignadas. Asigná left/rightPalmTransform.");

            _model = GetComponentInParent<Model>();
            if (_model) _model.OnWallrunStart += HandleWallrunStart;

            enterDistance = Mathf.Min(enterDistance, pickUpRange);
            exitDistance = Mathf.Max(exitDistance, enterDistance + 0.25f);
        }


        private void HandleWallrunStart(int dir)
        {
            if (!_objectGrabbable) return;

            Transform targetPalm = (dir < 0f) ? (rightPalmTransform ?? _currentPalm)
                : (dir > 0f) ? (leftPalmTransform ?? _currentPalm)
                : _currentPalm;

            if (targetPalm && targetPalm != _currentPalm)
            {
                _currentPalm = targetPalm;
                _objectGrabbable.MoveWhileHoldingToPalm(_currentPalm, palmHeight, handSwitchTime);
                var ownerRb = _model ? _model.rb : GetComponentInParent<Rigidbody>();
                _objectGrabbable.SetReferences(_currentPalm, ownerRb, transform);
            }
        }

        void Update()
        {
            UpdateInteractableFocus();

            GameObject hitObject = GetBestInteractable(out _, out _);

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!_objectGrabbable)
                {
                    if (hitObject)
                    {
                        Debug.Log("hit object");
                        var interactable = hitObject.GetComponentInParent<IInteractable>();
                        if (interactable != null)
                        {
                            OnInteractPerformed();
                            interactable.Interact();
                        }
                        else
                        {
                            TryGrabObject(hitObject);
                        }
                    }
                }
                else
                {
                    _isHoldingThrow = true;
                    holdTime = 0f;
                    throwCharge = 0f;
                }
            }

            if (_isHoldingThrow && _objectGrabbable)
            {
                holdTime += Time.deltaTime;
                throwCharge = holdTime;
                ThrowUISlider.Instance?.SetFill(Mathf.Clamp01(throwCharge / throwHoldThreshold));

                if (holdTime >= throwHoldThreshold)
                {
                    if (_objectGrabbable && !_objectGrabbable.IsOverlappingAnything)
                    {
                        MarkReleasing(_objectGrabbable);
                        _objectGrabbable.Throw(throwForce);
                        _objectGrabbable = null;
                        _isHoldingThrow = false;
                        holdTime = 0f;
                        throwCharge = 0f;
                        ThrowUISlider.Instance?.SetFill(0);
                        EventManager.TriggerEvent("OnObjectThrow", gameObject);
                    }
                }
            }

            if (Input.GetKeyUp(KeyCode.E) && _objectGrabbable && _isHoldingThrow)
            {
                if (holdTime < throwHoldThreshold) TryDropObject();

                _isHoldingThrow = false;
                holdTime = 0f;
                throwCharge = 0f;
            }

            if (_objectGrabbable) UpdateHolderPosition();
        }

        private void OnDisable()
        {
            if (_currentFocused)
            {
                OnInteractableFocusExit();
                _currentFocused = null;
                _pendingEnterTarget = null;
                _pendingEnterSince = -999f;
                _lostSince = -999f;
                _lastFocusHitDistance = Mathf.Infinity;
            }
        }

        private void UpdateInteractableFocus()
        {
            var best = GetBestInteractable(out float dist, out float angleDeg);

            bool canEnter = best &&
                            dist <= enterDistance &&
                            angleDeg <= enterAngleDeg &&
                            HasLineOfSight(best);

            bool mustExit = false;
            if (_currentFocused)
            {
                bool currentVisible = best == _currentFocused &&
                                      dist <= pickUpRange &&
                                      angleDeg <= exitAngleDeg &&
                                      HasLineOfSight(_currentFocused);

                mustExit = !currentVisible;
            }

            if (!_currentFocused)
            {
                if (canEnter)
                {
                    if (_pendingEnterTarget != best)
                    {
                        _pendingEnterTarget = best;
                        _pendingEnterSince = Time.time;
                    }
                    else if ((Time.time - _pendingEnterSince) >= enterStableTime)
                    {
                        _currentFocused = _pendingEnterTarget;
                        _pendingEnterTarget = null;
                        _pendingEnterSince = -999f;
                        _lostSince = -999f;
                        _lastFocusHitDistance = dist;
                        OnInteractableFocusEnter();
                    }
                }
                else
                {
                    _pendingEnterTarget = null;
                    _pendingEnterSince = -999f;
                }
            }
            else
            {
                if (mustExit)
                {
                    if (_lostSince < 0f) _lostSince = Time.time;
                    if ((Time.time - _lostSince) >= exitStableTime)
                    {
                        OnInteractableFocusExit();
                        _currentFocused = null;
                        _lostSince = -999f;
                        _pendingEnterTarget = null;
                        _pendingEnterSince = -999f;
                        _lastFocusHitDistance = Mathf.Infinity;
                    }
                }
                else
                {
                    _lostSince = -999f;
                    _lastFocusHitDistance = dist;
                }
            }
        }

        private struct TargetHit
        {
            public GameObject Go;
            public IInteractable Interactable;
            public PhysicsBox Box;
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private bool TryResolveTarget(Transform fromHit, out TargetHit target)
        {
            target = default;
            
            IInteractable ia = fromHit.GetComponentInParent<IInteractable>();
    
            if (ia == null) ia = fromHit.GetComponentInChildren<IInteractable>();

            if (ia != null)
            {
                var iaComp = (Component)ia;
                target.Go = iaComp.gameObject;
                target.Interactable = ia;
                return true;
            }

            PhysicsBox pb = fromHit.GetComponentInParent<PhysicsBox>();

            if (!pb) pb = fromHit.GetComponentInChildren<PhysicsBox>();
            
            if (!pb) return false;

            target.Go = pb.gameObject;
            target.Box = pb;
            return true;
        }

        private GameObject GetBestInteractable(out float distance, out float angleDeg)
        {
            Vector3 origin = transform.position + transform.forward * castStartOffset;
            Vector3 dir = transform.forward;

            distance = Mathf.Infinity;
            angleDeg = 999f;

            int targetMask = interactableMask | grabbableMask;

            // --- Raycast fino (primera prioridad)
            if (Physics.Raycast(origin, dir, out RaycastHit rh, exitDistance, targetMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (TryResolveTarget(rh.collider.transform, out var tg))
                {
                    distance = rh.distance;
                    angleDeg = 0f;
                    return tg.Go;
                }
            }

            // --- SphereCast (captura candidatos cercanos al centro de mira)
            RaycastHit[] hits = Physics.SphereCastAll(origin, focusSphereRadius, dir, exitDistance, targetMask,
                QueryTriggerInteraction.Ignore);

            GameObject best = null;
            float bestDist = Mathf.Infinity;
            float bestAngle = 999f;
            float maxAngle = (!_currentFocused) ? enterAngleDeg : exitAngleDeg;

            foreach (var h in hits)
            {
                if (!TryResolveTarget(h.collider.transform, out var tg)) continue;

                Vector3 targetPos = ClosestPointOrCenter(tg.Go, origin);
                Vector3 to = targetPos - origin;
                float d = to.magnitude;
                if (d <= 0.0001f) continue;

                float ang = Vector3.Angle(dir, to.normalized);
                if (ang > maxAngle) continue;

                if (d < bestDist && HasLineOfSight(tg.Go))
                {
                    best = tg.Go;
                    bestDist = d;
                    bestAngle = ang;
                }
            }

            distance = bestDist;
            angleDeg = bestAngle;
            return best;
        }

        private bool HasLineOfSight(GameObject targetRoot)
        {
            if (!targetRoot) return false;

            Vector3 origin = transform.position + transform.forward * castStartOffset;
            Vector3 targetPos = ClosestPointOrCenter(targetRoot, origin);

            int excludeTargets = interactableMask | grabbableMask;
            int losMask = environmentMask & ~excludeTargets;

            if (Physics.Linecast(origin, targetPos, out RaycastHit hit, losMask, QueryTriggerInteraction.Ignore))
            {
                if (debugFocus) Debug.DrawLine(origin, hit.point, Color.red, 0.05f);
                return false;
            }

            if (debugFocus) Debug.DrawLine(origin, targetPos, Color.green, 0.05f);
            return true;
        }

        private Vector3 ClosestPointOrCenter(GameObject go, Vector3 from)
        {
            var col = go.GetComponentInChildren<Collider>();
            if (col) return col.ClosestPoint(from);
            return go.transform.position;
        }

        private void TryGrabObject(GameObject hitObject)
        {
            OnGrabItem();
            StartCoroutine(WaitGrab(hitObject));
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator WaitGrab(GameObject hitObject)
        {
            yield return new WaitForSeconds(0.2f);
            if (!hitObject) yield break;

            PhysicsBox physicsObject = hitObject.GetComponentInParent<PhysicsBox>();

            if (physicsObject)
            {
                var ownerRb = GetComponentInParent<Model>()?.rb;

                physicsObject.SetReferences(_currentPalm, ownerRb, transform);

                physicsObject.BeginHoldSmooth(_currentPalm, palmHeight, pickupApproachTime);

                _objectGrabbable = physicsObject;
            }
        }

        private void TryDropOnDeath()
        {
            if (!_objectGrabbable) return;

            TryDropObject();
        }
        public void TryDropObject()
        {
            if (_objectGrabbable)
            {
                MarkReleasing(_objectGrabbable);
                if (!_objectGrabbable.IsFreezed)
                {
                    ThrowUISlider.Instance?.SetFill(0);
                    _objectGrabbable.EndHold();
                    _objectGrabbable.Drop();
                    _objectGrabbable = null;
                    EventManager.TriggerEvent("OnObjectDrop", gameObject);
                }
                else if (!_objectGrabbable.IsOverlappingAnything)
                {
                    _objectGrabbable.EndHold();
                    _objectGrabbable.Drop();
                    _objectGrabbable = null;
                    EventManager.TriggerEvent("OnObjectDrop", gameObject);
                }
            }
        }

        public void ClearReleasingTargetIf(GameObject go)
        {
            if (_releasingTarget && _releasingTarget.gameObject == go)
            {
                _releasingTarget = null;
                _releaseAimUntil = -999f;
            }
        }


        private void UpdateHolderPosition()
        {
            if (!_objectGrabbable || !_currentPalm) return;
            if (_objectGrabbable.IsApproachingHand) return; // no interferir durante blends

            Quaternion baseRot = Quaternion.LookRotation(_currentPalm.forward, _currentPalm.up);
            Quaternion spin = Quaternion.AngleAxis(Time.time * hoverSpinSpeed, _currentPalm.up);
            Quaternion targetRot = baseRot * spin;

            _objectGrabbable.transform.SetParent(_currentPalm, worldPositionStays: false);
            _objectGrabbable.transform.localPosition = Vector3.up * palmHeight;
            _objectGrabbable.transform.rotation = targetRot;

            _rotationSmoothQuat =
                Quaternion.Slerp(_rotationSmoothQuat, targetRot, Time.deltaTime * rotationSmoothSpeed);
        }

        public bool HasObjectInHand() => _objectGrabbable && _objectGrabbable.gameObject.activeInHierarchy;

        private void OnDrawGizmosSelected()
        {
            if (!debugFocus) return;
            Gizmos.color = Color.cyan;
            Vector3 origin = transform.position + transform.forward * castStartOffset;
            Gizmos.DrawWireSphere(origin, 0.03f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin, transform.forward * exitDistance);
        }
    }
}