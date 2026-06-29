using System;
using System.Collections;
using Audio.Scripts;
using Player.Stasis;
using Puzzle_Elements.AllInterfaces;
using UnityEngine;

namespace Puzzle_Elements.Hedron.Scripts
{
    public class PhysicsBox : MonoBehaviour, IStasis, IPlateActivator, ISoundPlayer
    {
        [SerializeField] private Renderer _renderer;

        [Header("Components")] 
        [SerializeField] private Collider mainCollider;

        public Rigidbody rb;

        private Transform _objGrabPointTransform;

        private bool _isFreezed;
        private bool _savedKinematic;
        private Vector3 _savedVelocity;
        private Vector3 _savedAngularVelocity;
        private float _savedDrag;
        private Transform _ownerForward;
        private Rigidbody _ownerRb;

        public bool IsOverlappingAnything { get; }
        public bool IsFreezed => _isFreezed;
        public StasisEffect StasisEffect { get; private set; }

        [SerializeField] private ParticleSystem particleFrozen;

        [Header("Hold FX")]
        [SerializeField] private float heldScaleFactor = 0.35f;
        [SerializeField] private float scaleLerpTime   = 0.18f;
        private Vector3 _originalScale;
        private bool _holding;

        public bool IsApproachingHand { get; private set; }

        [Header("Drop Nudge")]
        [SerializeField] private float dropNudgeImpulse = 1.1f;
        [SerializeField] private float dropNudgeUp      = 0.08f;
        [SerializeField] private float dropSeparation   = 0.18f;
        [SerializeField] private float throwSeparation  = 0.06f;
        
        [Header("Debug / Vectors")]
        [SerializeField] private bool  showDebugVectors = true;
        [SerializeField] private Color debugForwardColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color debugFreezeColor  = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private float debugArrowLen     = 0.75f;
        [SerializeField] private float debugDrawTime     = 10f;
        
        public Action OnCollision;
        public Action OnFreezeStart;
        public Action OnFreezeStop;
        public Action OnGrab;
        //
        [HideInInspector] public Vector3 posInitial;

        public PhysicsBox(bool isOverlappingAnything)
        {
            IsOverlappingAnything = isOverlappingAnything;
        }

        private void Start()
        {
            if (!_renderer) _renderer = GetComponent<Renderer>();

            if (!mainCollider) mainCollider = GetComponentInChildren<Collider>(true);

            _originalScale = transform.localScale;
            posInitial = transform.position;
            
            StasisEffect = new StasisEffect(_renderer);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (rb && rb.linearVelocity.magnitude != 0f)
            {
                OnCollision?.Invoke();
            }
        }

        public void Grab()
        {
            Debug.Log("grab2");
            OnGrab += () => Debug.Log("Grab");
            OnGrab?.Invoke();
            if (!_isFreezed)
            {
                _savedVelocity = Vector3.zero;
                _savedAngularVelocity = Vector3.zero;

            }
      
            gameObject.layer = _objGrabPointTransform.gameObject.layer;
            transform.parent = _objGrabPointTransform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
          
            rb.isKinematic = true;
            rb.useGravity = false;

            SetSelfColliderTrigger(true);
        }

        public void BeginHold()
        {
            if (_holding) return;
            _holding = true;

            StopAllCoroutines();
            StartCoroutine(LerpScale(transform.localScale, _originalScale * heldScaleFactor, scaleLerpTime));
            SetSelfColliderTrigger(true);
        }

        public void BeginHoldSmooth(Transform palm, float palmHeight, float approachTime)
        {
            if (!palm) return;

            StopAllCoroutines();
            IsApproachingHand = true;
            _holding = true;

            rb.isKinematic = true;
            rb.useGravity  = false;
            SetSelfColliderTrigger(true);
            OnGrab?.Invoke();
            _objGrabPointTransform = palm;

            StartCoroutine(Co_ApproachAndScale(palm, palmHeight, approachTime));
        }

        private IEnumerator Co_ApproachAndScale(Transform palm, float palmHeight, float tDur)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 startScale = transform.localScale;

            Vector3 endPos = palm.position + palm.up * palmHeight;
            Quaternion endRot = Quaternion.LookRotation(palm.forward, palm.up);
            Vector3 endScale = _originalScale * heldScaleFactor;

            float t = 0f;
            tDur = Mathf.Max(0.0001f, tDur);

            while (t < 1f)
            {
                t += Time.deltaTime / tDur;
                float e = Mathf.SmoothStep(0f, 1f, t);

                transform.position   = Vector3.Lerp(startPos, endPos, e);
                transform.rotation   = Quaternion.Slerp(startRot, endRot, e);
                transform.localScale = Vector3.Lerp(startScale, endScale, e);

                yield return null;
            }

            transform.SetParent(palm, worldPositionStays: false);
            transform.localPosition = Vector3.up * palmHeight;
            transform.localRotation = Quaternion.identity;

            IsApproachingHand = false;
        }

        public void MoveWhileHoldingToPalm(Transform newPalm, float palmHeight, float tDur)
        {
            if (!newPalm) return;

            StopAllCoroutines();
            IsApproachingHand = true;

            StartCoroutine(Co_MoveWhileHolding(newPalm, palmHeight, tDur));
        }

        private IEnumerator Co_MoveWhileHolding(Transform newPalm, float palmHeight, float tDur)
        {
            Transform oldParent = transform.parent;
            transform.SetParent(null, true);

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 currentScale = transform.localScale;

            Vector3 endPos = newPalm.position + newPalm.up * palmHeight;
            Quaternion endRot = Quaternion.LookRotation(newPalm.forward, newPalm.up);

            float t = 0f;
            tDur = Mathf.Max(0.0001f, tDur);

            while (t < 1f)
            {
                t += Time.deltaTime / tDur;
                float e = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPos, endPos, e);
                transform.rotation = Quaternion.Slerp(startRot, endRot, e);
                transform.localScale = currentScale; // fija

                yield return null;
            }

            // actualizar referencia y re-parentar a la nueva palma
            _objGrabPointTransform = newPalm;
            transform.SetParent(newPalm, worldPositionStays: false);
            transform.localPosition = Vector3.up * palmHeight;
            transform.localRotation = Quaternion.identity;

            IsApproachingHand = false;
        }

        public void EndHold()
        {
            if (!_holding) return;
            _holding = false;

            StopAllCoroutines();
            StartCoroutine(LerpScale(transform.localScale, _originalScale, scaleLerpTime));

            SetSelfColliderTrigger(false);
        }

        private IEnumerator LerpScale(Vector3 from, Vector3 to, float time)
        {
            float t = 0f;
            float dur = Mathf.Max(0.0001f, time);
            while (t < 1f)
            {
                t += Time.deltaTime / dur;
                transform.localScale = Vector3.Lerp(from, to, t);
                yield return null;
            }
            transform.localScale = to;
        }

        private void SetSelfColliderTrigger(bool asTrigger)
        {
            if (!mainCollider) return;
            mainCollider.isTrigger = asTrigger;
            mainCollider.enabled   = true;
        }

        public void Drop(float separationOverride = -1f, bool applyNudge = true)
        {
            transform.parent = null;
            gameObject.layer = LayerMask.NameToLayer("Physics Objects");

            if (!_isFreezed)
            {
                rb.isKinematic = false;
                rb.useGravity  = true;

                // base en el forward del player (dueño); fallbacks seguros
                Vector3 fwd = _ownerForward ? _ownerForward.forward
                    : _objGrabPointTransform ? _objGrabPointTransform.forward
                    : transform.forward;

                Vector3 up  = _ownerForward ? _ownerForward.up : Vector3.up;
                Vector3 dir = (fwd + up * 0.25f).normalized;

                float sep = (separationOverride >= 0f) ? separationOverride : dropSeparation;

                Vector3 startPos = (_objGrabPointTransform ? _objGrabPointTransform.position : transform.position) + dir * sep;
                transform.position = startPos;
                
                DebugOwnerForwardSource("Drop()");
                DrawArrow(startPos, dir, debugForwardColor, debugArrowLen, debugDrawTime);

                Vector3 ownerVel = Vector3.zero;
                if (_ownerRb)
                {
                    ownerVel = _ownerRb.linearVelocity;
                    ownerVel.y = 0f;
                }

                rb.WakeUp();
                rb.linearVelocity = ownerVel;

                // nudge opcional (solo para Drop normal; en Throw lo desactivamos)
                if (applyNudge)
                {
                    Vector3 nudge = dir * dropNudgeImpulse + Vector3.up * dropNudgeUp;
                    rb.AddForce(nudge, ForceMode.Impulse);
                }
            }
            else
            {
                // Estando freezeado: mantener magnitud pero reorientar al forward del player al momento de soltar
                float speed = _savedVelocity.magnitude;
                UpdateSavedVelocityDirection(speed);
            }

            SetSelfColliderTrigger(false);
        }

        public void Throw(float force)
        {
            EndHold();
            // En throw, separamos un poco y no aplicamos nudge extra
            Drop(separationOverride: throwSeparation, applyNudge: false);

            if (_isFreezed)
            {
                // Sigue freezeado: preparar la velocidad guardada con magnitud según el throw y dirección del player
                float plannedSpeed = (rb ? (force / Mathf.Max(0.001f, rb.mass)) : _savedVelocity.magnitude);
                UpdateSavedVelocityDirection(plannedSpeed);
                return;
            }

            // No freezeado: impulso real ahora
            Vector3 fwd = GetOwnerForward();
            Vector3 impulse = fwd * force;
            
            DebugOwnerForwardSource("Throw()");
            DrawArrow(transform.position, fwd, debugForwardColor, debugArrowLen, debugDrawTime);
            
            rb.AddForce(impulse, ForceMode.Impulse);
        }

        public void SetReferences(Transform grabHolder, Rigidbody ownerRb, Transform ownerForward = null)
        {
            _objGrabPointTransform = grabHolder;
            _ownerForward = ownerForward ? ownerForward : grabHolder; // fallback seguro
            _ownerRb = ownerRb;
        }

        public void StatisEffectActivate()  => FreezeObject();
        public void StatisEffectDeactivate()=> UnfreezeObject();

        private void FreezeObject()
        {
            if (_isFreezed) return;

            OnFreezeStart?.Invoke();
            SaveObjectState();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
            _isFreezed = true;
            StasisEffect.StasisEffectStart();
        }

        private void SaveObjectState()
        {
            if (!rb) return;
            _savedKinematic = rb.isKinematic;
            _savedVelocity = rb.linearVelocity;
            _savedAngularVelocity = rb.angularVelocity;
            _savedDrag = rb.linearDamping;
        }

        private void RestoreObjectState()
        {
            if (!rb) return;
            rb.isKinematic = _savedKinematic;
            rb.linearVelocity = _savedVelocity;
            rb.angularVelocity = _savedAngularVelocity;
            rb.linearDamping = _savedDrag;
            rb.WakeUp();
        }

        private void UnfreezeObject()
        {
            if (!_isFreezed) return;
            
            OnFreezeStop?.Invoke();
            RestoreObjectState();
            _isFreezed = false;
            rb.useGravity = true;
            rb.isKinematic = false;
            StasisEffect.StasisEffectStop();
        }
        
        private Vector3 GetOwnerForward()
        {
            if (_ownerForward) return _ownerForward.forward;
            if (_objGrabPointTransform) return _objGrabPointTransform.forward;
            return transform.forward;
        }
        
        private void UpdateSavedVelocityDirection(float newSpeed)
        {
            float speed = (newSpeed >= 0f) ? newSpeed : _savedVelocity.magnitude;
            Vector3 fwd = GetOwnerForward();
            _savedVelocity = fwd.normalized * speed;

            DrawArrow(transform.position, fwd, debugFreezeColor, debugArrowLen, debugDrawTime);
            DebugOwnerForwardSource("UpdateSavedVelocityDirection()");
        }

        private void DrawArrow(Vector3 origin, Vector3 dir, Color color, float length, float time)
        {
            if (!showDebugVectors) return;

            Vector3 a = origin;
            Vector3 b = origin + dir.normalized * length;

            Debug.DrawRay(a, (b - a), color, time);

            Vector3 right = Vector3.Cross(dir.normalized, Vector3.up).normalized;
            Vector3 tip   = b;
            float headLen = length * 0.18f;
            Debug.DrawRay(tip, (-dir.normalized + right)  * headLen, color, time);
            Debug.DrawRay(tip, (-dir.normalized - right)  * headLen, color, time);
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private void DebugOwnerForwardSource(string ctx)
        {
            if (!showDebugVectors) return;

            string src = _ownerForward ? $"ownerForward (Transform: {_ownerForward.name})"
                : _objGrabPointTransform ? $"grabPoint (Transform: {_objGrabPointTransform.name})"
                : "self (transform.forward)";

            Debug.Log($"[PhysicsBox] {ctx} | forward source = {src}", this);
        }
    }
}