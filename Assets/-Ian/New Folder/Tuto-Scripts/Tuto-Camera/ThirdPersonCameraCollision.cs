using UnityEngine;

public sealed class ThirdPersonCameraCollision : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Third-person camera controlled by this component.")]
    [SerializeField] private Camera targetCamera;

    [Header("Camera Position")]
    [Tooltip("Normal distance of the camera behind the pivot.")]
    [SerializeField, Min(0.1f)] private float desiredDistance = 4f;

    [Tooltip("Minimum distance allowed between the pivot and the camera.")]
    [SerializeField, Min(0.05f)] private float minimumDistance = 0.2f;

    [Tooltip("Vertical offset of the third-person camera.")]
    [SerializeField] private float verticalOffset = 0.4f;

    [Header("Collision")]
    [Tooltip("Layers capable of blocking the camera.")]
    [SerializeField] private LayerMask collisionMask = ~0;

    [Tooltip("Radius used by the collision sphere.")]
    [SerializeField, Range(0.01f, 1f)] private float collisionRadius = 0.2f;

    [Tooltip("Extra separation maintained from collision surfaces.")]
    [SerializeField, Range(0f, 0.5f)] private float collisionPadding = 0.05f;

    [Header("Return")]
    [Tooltip(
        "Speed at which the camera returns to its normal distance. " +
        "Moving toward an obstruction is intentionally instantaneous.")]
    [SerializeField, Min(0f)] private float returnSpeed = 8f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    [Tooltip("Print detected collision information.")]
    [SerializeField] private bool logDetectedColliders;

    private Transform cameraTransform;

    private float currentDistance;

    private Vector3 previousCameraPosition;

    // Debug information.
    private Vector3 lastRadialOrigin;
    private Vector3 lastRadialEnd;

    private Vector3 lastSweepOrigin;
    private Vector3 lastSweepEnd;

    private bool lastRadialHit;
    private bool lastSweepHit;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (cameraTransform == null)
        {
            Initialize();
            return;
        }

        previousCameraPosition = cameraTransform.position;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            return;
        }

        UpdateCameraCollision();
    }

    private void Initialize()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>(true);
        }

        if (targetCamera == null)
        {
            Debug.LogError(
                $"{nameof(ThirdPersonCameraCollision)} on '{gameObject.name}' " +
                "requires a Camera reference.",
                this);

            enabled = false;
            return;
        }

        cameraTransform = targetCamera.transform;

        currentDistance = desiredDistance;

        Vector3 initialPosition = CalculateWorldPosition(desiredDistance);

        cameraTransform.position = initialPosition;
        cameraTransform.rotation = transform.rotation;

        previousCameraPosition = initialPosition;
    }

    private void UpdateCameraCollision()
    {
        /*
         * STEP 1
         * ---------------------------------------------------------
         * Calculate the normal desired camera trajectory.
         */

        Vector3 pivotPosition = transform.position;

        Vector3 desiredWorldPosition =
            transform.TransformPoint(
                new Vector3(
                    0f,
                    verticalOffset,
                    -desiredDistance
                )
            );

        Vector3 radialDisplacement =
            desiredWorldPosition - pivotPosition;

        float radialLength =
            radialDisplacement.magnitude;

        if (radialLength <= Mathf.Epsilon)
        {
            return;
        }

        Vector3 radialDirection =
            radialDisplacement / radialLength;

        lastRadialOrigin = pivotPosition;
        lastRadialEnd = desiredWorldPosition;
        lastRadialHit = false;
        lastSweepHit = false;

        /*
         * STEP 2
         * ---------------------------------------------------------
         * Radial collision:
         *
         * Pivot -------> Desired Camera
         *
         * This handles walls between the player and the desired
         * camera position.
         */

        float safeRadialDistance = radialLength;

        if (Physics.SphereCast(
                pivotPosition,
                collisionRadius,
                radialDirection,
                out RaycastHit radialHit,
                radialLength,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            lastRadialHit = true;

            safeRadialDistance = Mathf.Max(
                minimumDistance,
                radialHit.distance - collisionPadding
            );

            if (logDetectedColliders)
            {
                Debug.Log(
                    $"[TPS Camera] Radial hit: {radialHit.collider.name} | " +
                    $"Layer: {LayerMask.LayerToName(radialHit.collider.gameObject.layer)} | " +
                    $"Hit: {radialHit.distance:F3} | " +
                    $"Safe: {safeRadialDistance:F3}",
                    radialHit.collider
                );
            }
        }

        /*
         * STEP 3
         * ---------------------------------------------------------
         * Do NOT smooth movement toward a wall.
         *
         * If the allowed distance suddenly becomes smaller,
         * the camera must immediately move to safety.
         *
         * We only smooth the return outward.
         */

        if (safeRadialDistance < currentDistance)
        {
            currentDistance = safeRadialDistance;
        }
        else
        {
            currentDistance = Mathf.MoveTowards(
                currentDistance,
                safeRadialDistance,
                returnSpeed * Time.deltaTime
            );
        }

        /*
         * The safe candidate lies on exactly the same trajectory
         * that was tested by the radial SphereCast.
         */

        Vector3 candidatePosition =
            pivotPosition +
            radialDirection * currentDistance;

        /*
         * STEP 4
         * ---------------------------------------------------------
         * Orbital collision.
         *
         * The radial cast alone is insufficient while rotating.
         *
         * Example:
         *
         * PreviousCamera ---> CandidateCamera
         *
         * We sweep the collision sphere through the actual movement
         * performed by the camera during this frame.
         */

        Vector3 sweepDisplacement =
            candidatePosition - previousCameraPosition;

        float sweepDistance =
            sweepDisplacement.magnitude;

        lastSweepOrigin = previousCameraPosition;
        lastSweepEnd = candidatePosition;

        Vector3 finalPosition = candidatePosition;

        if (sweepDistance > Mathf.Epsilon)
        {
            Vector3 sweepDirection =
                sweepDisplacement / sweepDistance;

            if (Physics.SphereCast(
                    previousCameraPosition,
                    collisionRadius,
                    sweepDirection,
                    out RaycastHit sweepHit,
                    sweepDistance,
                    collisionMask,
                    QueryTriggerInteraction.Ignore))
            {
                lastSweepHit = true;

                float safeSweepDistance =
                    Mathf.Max(
                        0f,
                        sweepHit.distance - collisionPadding
                    );

                finalPosition =
                    previousCameraPosition +
                    sweepDirection * safeSweepDistance;

                /*
                 * Keep currentDistance synchronized with the actual
                 * resulting camera position.
                 */
                currentDistance = Vector3.Distance(
                    pivotPosition,
                    finalPosition
                );

                currentDistance = Mathf.Max(
                    minimumDistance,
                    currentDistance
                );

                if (logDetectedColliders)
                {
                    Debug.Log(
                        $"[TPS Camera] Orbital hit: {sweepHit.collider.name} | " +
                        $"Layer: {LayerMask.LayerToName(sweepHit.collider.gameObject.layer)} | " +
                        $"Hit: {sweepHit.distance:F3}",
                        sweepHit.collider
                    );
                }
            }
        }

        /*
         * STEP 5
         * ---------------------------------------------------------
         * Apply the final world-space transform.
         */

        cameraTransform.position = finalPosition;
        cameraTransform.rotation = transform.rotation;

        previousCameraPosition = finalPosition;
    }

    private Vector3 CalculateWorldPosition(float distance)
    {
        Vector3 localPosition = new Vector3(
            0f,
            verticalOffset,
            -distance
        );

        return transform.TransformPoint(localPosition);
    }

    private void OnValidate()
    {
        desiredDistance =
            Mathf.Max(0.1f, desiredDistance);

        minimumDistance =
            Mathf.Clamp(
                minimumDistance,
                0.05f,
                desiredDistance
            );

        collisionRadius =
            Mathf.Max(
                0.01f,
                collisionRadius
            );

        collisionPadding =
            Mathf.Max(
                0f,
                collisionPadding
            );

        returnSpeed =
            Mathf.Max(
                0f,
                returnSpeed
            );
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        /*
         * Desired radial trajectory.
         */

        Vector3 desiredPosition =
            transform.TransformPoint(
                new Vector3(
                    0f,
                    verticalOffset,
                    -desiredDistance
                )
            );

        Gizmos.DrawLine(
            transform.position,
            desiredPosition
        );

        Gizmos.DrawWireSphere(
            desiredPosition,
            collisionRadius
        );

        if (!Application.isPlaying)
        {
            return;
        }

        /*
         * Radial test.
         */

        Gizmos.DrawLine(
            lastRadialOrigin,
            lastRadialEnd
        );

        /*
         * Actual frame-to-frame orbital sweep.
         */

        Gizmos.DrawLine(
            lastSweepOrigin,
            lastSweepEnd
        );

        Gizmos.DrawWireSphere(
            lastSweepOrigin,
            collisionRadius
        );

        Gizmos.DrawWireSphere(
            lastSweepEnd,
            collisionRadius
        );
    }
}