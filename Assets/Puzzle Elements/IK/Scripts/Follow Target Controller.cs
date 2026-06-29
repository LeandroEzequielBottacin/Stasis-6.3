using System;
using System.Collections;
using Managers.Game;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Puzzle_Elements.IK.Scripts
{
    [RequireComponent(typeof(Rigidbody))]
    public class FollowTargetController : MonoBehaviour
    {
        // =========================
        // REFERENCES
        // =========================
        [Header("References")]
        [Tooltip("Player transform used to measure distance and drive the rig weight.")]
        public Transform player;

        [Tooltip("Rig from Animation Rigging package whose weight will be driven (0..1).")]
        public Rig rig;

        [Tooltip("Optional second anchor (target B). ChangePosition() toggles between StartAnchor (A) and this Transform (B).")]
        public Transform brother;

        [Space(4)]
        [Tooltip("If true, allows changing layers/tags on the platform object (editor-side toggle).")]
        [SerializeField] private bool isTagModifiable;

        [Tooltip("Layer to assign when the current tip is the StartAnchor (controller mode). Use a single layer, not a mask.")]
        [SerializeField] private LayerMask _layerTipController;

        [Tooltip("Layer to assign when the current tip is 'brother' (brother mode). Use a single layer, not a mask.")]
        [SerializeField] private LayerMask _layerBrother;

        [Tooltip("Root GameObject whose layer is changed recursively when toggling between controller and brother.")]
        [SerializeField] private GameObject _platformModifyTag;

        [Space(4)]
        [Tooltip("Optional mid waypoint used when moving from BROTHER -> START (segment A: mid1).")]
        public Transform mid1;

        [Tooltip("Optional mid waypoint used when moving from START -> BROTHER (segment B: mid2).")]
        public Transform mid2;

        // =========================
        // RIG WEIGHT BY DISTANCE
        // =========================
        [Header("Rig Weight by Distance (remap)")]
        [Tooltip("Input distance range: distance <= inMin -> outMin; distance >= inMax -> outMax.")]
        [Min(0f)] public float inMin = 2f;

        [Tooltip("Input distance range: distance <= inMin -> outMin; distance >= inMax -> outMax.")]
        [Min(0f)] public float inMax = 5f;

        [Tooltip("Output range min for the remap (usually 0).")]
        [Range(0f, 1f)] public float outMin;

        [Tooltip("Output range max for the remap (usually 1).")]
        [Range(0f, 1f)] public float outMax = 1f;

        [Tooltip("Curve applied after remap (x: 0..1 input, y: 0..1 output).")]
        public AnimationCurve remapLerp = AnimationCurve.Linear(0, 0, 1, 1);

        // =========================
        // DISTANCE STABILITY / FILTER
        // =========================
        [Header("Distance Filtering & Stability")]
        [Tooltip("If true, ignores vertical delta (Y) when measuring distance.")]
        public bool onlyHorizontalDistance = true;

        [Tooltip("Exponential smoothing in Hz for distance (0 = no smoothing). Typical: 8..20 Hz.")]
        [Min(0f)] public float distanceSmoothHz = 12f;

        [Tooltip("Dead zone for distance updates (meters). Differences below this are ignored.")]
        [Min(0f)] public float distanceDeadZone = 0.03f;

        [Tooltip("Dead zone for rig weight changes. Differences below this are ignored.")]
        [Range(0f, 1f)] public float weightDeadZone = 0.01f;

        [Tooltip("SmoothDamp time for rig weight. Lower = snappier.")]
        [Min(0f)] public float weightSmoothTime = 0.15f;

        // =========================
        // POSITION CHANGE (TOGGLE A <-> B)
        // =========================
        [Header("ChangePosition Movement")]
        [Tooltip("Total duration for the coroutine movement when toggling between anchors.")]
        [Min(0f)] public float moveDuration = 1f;

        // =========================
        // SAFETY / MOTION CAPS
        // =========================
        [Header("Movement Safety")]
        [Tooltip("Maximum linear speed allowed while following the path (m/s).")]
        [Min(0f)] public float maxMoveSpeed = 5f;

        // =========================
        // RUNTIME CONTROL
        // =========================
        [Header("Runtime Control")]
        [Tooltip("If false, motion and weight updates pause (useful for cutscenes/pauses).")]
        public bool canMove = true;

        // =========================
        // BROTHER BEHAVIOR
        // =========================
        [Header("Brother Settings")]
        [Tooltip("When targeting 'brother', the rig weight will never go below this min, rising smoothly to it.")]
        [Range(0f, 1f)] public float brotherMinWeight = 0.6f;

        [Tooltip("Time (seconds) for the min-weight ramp when approaching the brother anchor.")]
        [Min(0f)] public float brotherApproachSmooth = 0.2f;

        // =========================
        // REFERENCE FRAME
        // =========================
        [Header("Reference Frame")]
        [Tooltip("ON: StartAnchor and computations live in parent frame (e.g., a moving train). OFF: world space.")]
        public bool anchorInParentFrame = true;

        [Tooltip("Optional explicit frame. If null, uses transform.parent (or this transform if no parent).")]
        public Transform frame;

        // =========================
        // INTERNAL / RUNTIME (read-only at runtime)
        // =========================
        private Rigidbody rb;
        private Transform startAnchor;
        [Tooltip("Current destination tip (StartAnchor or Brother).")]
        public Transform currentTip;

        private bool atStart = true;
        private Coroutine moveRoutine;

        private float targetWeight;
        private float currentWeight;
        private float weightVel;

        private float distRaw;
        private float distFiltered;

        [Tooltip("Smoothed distance (public get).")]
        public float dist { get; private set; }

        // Anchor snapshots
        private Vector3 aPosWorld;
        private Quaternion aRotWorld;
        private Vector3 aPosLocal;
        private Quaternion aRotLocal;

        private float brotherCurrentMin;

        public Action OnArmMove;
        [Header("Eventos de movimiento")]
        [SerializeField] private float armMoveSignificantDelta = 0.05f;
        private bool armMovingNotified;

        // -------------------------
        // Helpers
        // -------------------------
        private Transform GetFrame()
        {
            if (frame) return frame;
            if (transform.parent) return transform.parent;
            return transform;
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        private void Start()
        {
            // Auto-assign player if GameManager provides one
            if (GameManager.Instance != null && player == null)
                player = GameManager.Instance.player;

            // Create StartAnchor at current pose
            startAnchor = new GameObject(name + "_StartAnchor").transform;

            if (anchorInParentFrame)
            {
                Transform f = GetFrame();
                startAnchor.SetParent(f, true);
                startAnchor.SetPositionAndRotation(transform.position, transform.rotation);

                aPosLocal = f.InverseTransformPoint(transform.position);
                aRotLocal = Quaternion.Inverse(f.rotation) * transform.rotation;

                // Physics interpolation off when we are in parent frame to avoid double interpolation artifacts
                rb.interpolation = RigidbodyInterpolation.None;
            }
            else
            {
                startAnchor.SetParent(null, true);
                startAnchor.SetPositionAndRotation(transform.position, transform.rotation);

                aPosWorld = transform.position;
                aRotWorld = transform.rotation;

                // World-space: allow interpolation for smoother visuals
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            currentTip = startAnchor;
            atStart = true;

            // Initialize rig weight from current value
            if (rig != null)
            {
                currentWeight = Mathf.Clamp01(rig.weight);
                if (currentWeight > 0.95f) currentWeight = 1f; // snap to 1
                targetWeight = currentWeight;
                rig.weight = currentWeight;
            }

            // Seed distance filters
            if (player != null)
            {
                Vector3 d = player.position - transform.position;
                if (onlyHorizontalDistance) d.y = 0f;
                distRaw = distFiltered = d.magnitude;
            }
        }

        private void FixedUpdate()
        {
            // Keep RB pose coherent when working in parent frame
            if (anchorInParentFrame)
            {
                rb.position = transform.position;
                rb.rotation = transform.rotation;
            }

            // Distance tracking & smoothing
            if (player != null)
            {
                Vector3 delta = player.position - rb.position;
                if (onlyHorizontalDistance) delta.y = 0f;

                float newDist = delta.magnitude;

                if (Mathf.Abs(newDist - distFiltered) > distanceDeadZone)
                    distRaw = newDist;

                if (distanceSmoothHz > 0f)
                {
                    float alpha = 1f - Mathf.Exp(-distanceSmoothHz * Time.deltaTime);
                    distFiltered = Mathf.Lerp(distFiltered, distRaw, alpha);
                }
                else
                {
                    distFiltered = distRaw;
                }

                dist = distFiltered;
            }

            if (!canMove) return;

            // Remap distance -> (0..1), then curve
            float raw = math.remap(inMin, inMax, outMin, outMax, distFiltered);
            float curved = remapLerp.Evaluate(raw);

            // Special behavior when current tip is 'brother'
            if (currentTip == startAnchor)
            {
                targetWeight = Mathf.Clamp01(curved);
                brotherCurrentMin = 0f;
            }
            else if (currentTip == brother)
            {
                brotherCurrentMin = Mathf.MoveTowards(
                    brotherCurrentMin,
                    brotherMinWeight,
                    Time.fixedDeltaTime / Mathf.Max(0.0001f, brotherApproachSmooth)
                );

                targetWeight = Mathf.Max(curved, brotherCurrentMin);
            }
            else
            {
                targetWeight = Mathf.Clamp01(curved);
                brotherCurrentMin = 0f;
            }

            // Dead zone for weight
            float desired = targetWeight;
            if (Mathf.Approximately(desired, currentWeight) || Mathf.Abs(desired - currentWeight) < weightDeadZone)
                desired = currentWeight;

            float deltaWeight = Mathf.Abs(desired - currentWeight);
            bool isMovingSignificantly = deltaWeight >= armMoveSignificantDelta;

            if (isMovingSignificantly && !armMovingNotified)
            {
                OnArmMove?.Invoke();
                armMovingNotified = true;
            }
            else if (!isMovingSignificantly && armMovingNotified)
            {
                armMovingNotified = false;
            }
            // ===============================================================

            // Smooth weight to rig
            if (rig != null)
            {
                currentWeight = Mathf.SmoothDamp(
                    currentWeight,
                    desired,
                    ref weightVel,
                    weightSmoothTime,
                    Mathf.Infinity,
                    Time.deltaTime
                );
                currentWeight = Mathf.Clamp01(currentWeight);
                if (currentWeight > 0.95f) currentWeight = 1f; // snap to 1
                rig.weight = currentWeight;
            }

            // Apply path following based on weight
            ApplyPathByWeight();
        }

        /// <summary>
        /// Forces a toggle on next ChangePosition call (sets atStart=false) and moves.
        /// </summary>
        public void ResetObject()
        {
            atStart = false;
            ChangePosition();
        }


        public void ChangePositionToBrother()
        {
            Transform to = brother;
            currentTip = startAnchor;
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveRB_Pausable(to, moveDuration));
            OnArmMove?.Invoke();
        }
        public void ChangePositionToStartAnchor()
        {
            Transform to = startAnchor;
            currentTip = brother;
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveRB_Pausable(to, moveDuration));
            OnArmMove?.Invoke();
        }
        /// <summary>
        /// Toggles target tip between StartAnchor (A) and Brother (B), running a timed move.
        /// </summary>
        public void ChangePosition()
        {
            if (brother == null) return;

            Transform to = atStart ? brother : startAnchor;

            currentTip = to;

            // NOTE: layer switching kept commented out but tooltipped above for clarity.
            // if (currentTip == brother) SetLayerRecursively(_platformModifyTag, _layerBrother);
            // else SetLayerRecursively(_platformModifyTag, _layerTipController);

            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveRB_Pausable(to, moveDuration));
            OnArmMove?.Invoke();
        }

        // Recursive layer assign (expects a single bit in LayerMask)
        void SetLayerRecursively(GameObject parent, LayerMask layerMask)
        {
            int newLayer = Mathf.RoundToInt(Mathf.Log(layerMask.value, 2));

            if (newLayer < 0 || newLayer > 31)
            {
                Debug.LogError("Invalid LayerMask. Pass exactly one layer (single bit).");
                return;
            }

            parent.layer = newLayer;

            foreach (Transform child in parent.transform)
                SetLayerRecursively(child.gameObject, layerMask);
        }

        // Time-slice coroutine that pauses with canMove=false and resumes where it left off
        private IEnumerator MoveRB_Pausable(Transform to, float totalDuration)
        {
            totalDuration = Mathf.Max(0.0001f, totalDuration);
            float remaining = totalDuration;

            Vector3 segStartPos = rb.position;
            Quaternion segStartRot = rb.rotation;

            while (remaining > 0f)
            {
                while (!canMove) yield return null;

                Vector3 segEndPos = to.position;
                Quaternion segEndRot = to.rotation;

                float elapsed = 0f;
                while (elapsed < remaining && canMove)
                {
                    float u = Mathf.Clamp01(elapsed / remaining);
                    float k = remapLerp.Evaluate(u);

                    segEndPos = to.position;
                    segEndRot = to.rotation;

                    rb.MovePosition(Vector3.LerpUnclamped(segStartPos, segEndPos, k));
                    rb.MoveRotation(Quaternion.SlerpUnclamped(segStartRot, segEndRot, k));

                    elapsed += Time.fixedDeltaTime;
                    yield return new WaitForFixedUpdate();
                }

                if (!canMove)
                {
                    remaining -= elapsed;
                    segStartPos = rb.position;
                    segStartRot = rb.rotation;
                    continue;
                }

                rb.MovePosition(segEndPos);
                rb.MoveRotation(segEndRot);
                remaining = 0f;
            }

            atStart = (to == startAnchor);

            // Save new anchor snapshot
            if (anchorInParentFrame)
            {
                Transform f = GetFrame();
                aPosLocal = f.InverseTransformPoint(rb.position);
                aRotLocal = Quaternion.Inverse(f.rotation) * rb.rotation;
            }
            else
            {
                aPosWorld = rb.position;
                aRotWorld = rb.rotation;
            }

            moveRoutine = null;
        }

        // Follows a two-segment path A->mid->dest driven by currentWeight (0..1)
        private void ApplyPathByWeight()
        {
            Transform dest = currentTip != null ? currentTip : (brother != null ? brother : startAnchor);
            if (dest == null) return;

            // Reconstruct anchor A pose from stored snapshot
            Vector3 aPos;
            Quaternion aRot;
            if (anchorInParentFrame)
            {
                Transform f = GetFrame();
                aPos = f.TransformPoint(aPosLocal);
                aRot = f.rotation * aRotLocal;
            }
            else
            {
                aPos = aPosWorld;
                aRot = aRotWorld;
            }

            Vector3 destPos = dest.position;
            Quaternion destRot = dest.rotation;

            // Choose mid based on direction (toB means going back to StartAnchor)
            bool toB = (dest == startAnchor);
            Transform midT = toB ? mid1 : mid2;

            Vector3 midPos;
            Quaternion midRot;

            if (midT != null)
            {
                midPos = midT.position;
                midRot = midT.rotation;
            }
            else
            {
                Vector3 refPos = toB ? startAnchor.position : (brother != null ? brother.position : destPos);
                Quaternion refRot = toB ? startAnchor.rotation : (brother != null ? brother.rotation : destRot);
                midPos = 0.5f * (aPos + refPos);
                midRot = Quaternion.Slerp(aRot, refRot, 0.5f);
            }

            float t = Mathf.Clamp01(currentWeight);

            Vector3 p;
            Quaternion r;

            if (t <= 0.5f)
            {
                float u = t * 2f;
                p = Vector3.LerpUnclamped(aPos, midPos, u);
                r = Quaternion.SlerpUnclamped(aRot, midRot, u);
            }
            else
            {
                float u = (t - 0.5f) * 2f;
                p = Vector3.LerpUnclamped(midPos, destPos, u);
                r = Quaternion.SlerpUnclamped(midRot, destRot, u);
            }

            // Cap step to avoid tunneling or excessive jumps
            Vector3 desiredMove = p - rb.position;
            float maxStep = maxMoveSpeed * Time.fixedDeltaTime;

            if (desiredMove.magnitude > maxStep)
            {
                desiredMove = desiredMove.normalized * maxStep;
                p = rb.position + desiredMove;
            }

            rb.MovePosition(p);
            rb.MoveRotation(r);
        }

        private void OnDisable()
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }
        }
    }
}
