using UnityEngine;

public sealed class FirstPersonCameraCollision : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Collision")]
    [Tooltip("Layers that can block the first-person camera.")]
    [SerializeField] private LayerMask collisionMask = ~0;

    [Tooltip("Radius used to prevent the camera near plane from entering geometry.")]
    [SerializeField, Min(0.01f)] private float collisionRadius = 0.08f;

    [Tooltip("Maximum amount the camera can be pushed backward.")]
    [SerializeField, Min(0f)] private float maxPushbackDistance = 0.25f;

    [Tooltip("Extra separation from detected geometry.")]
    [SerializeField, Min(0f)] private float collisionPadding = 0.02f;

    [Header("Smoothing")]
    [SerializeField, Min(0f)] private float pushbackSpeed = 30f;

    [SerializeField, Min(0f)] private float returnSpeed = 15f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos;

    private Transform cameraTransform;

    private Vector3 defaultLocalPosition;
    private float currentPushback;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>(true);
        }

        if (targetCamera == null)
        {
            Debug.LogError(
                $"{nameof(FirstPersonCameraCollision)} on '{gameObject.name}' requires a Camera.",
                this
            );

            enabled = false;
            return;
        }

        cameraTransform = targetCamera.transform;
        defaultLocalPosition = cameraTransform.localPosition;
    }

    private void LateUpdate()
    {
        HandleCollision();
    }

    private void HandleCollision()
    {
        Vector3 origin = transform.position;

        /*
         * We push the camera backward relative to the pivot.
         * This is mainly useful when the player is extremely close
         * to a wall and the camera's near plane would otherwise clip.
         */
        Vector3 backwardDirection = -transform.forward;

        float targetPushback = 0f;

        if (Physics.SphereCast(
                origin,
                collisionRadius,
                transform.forward,
                out RaycastHit hit,
                collisionRadius + collisionPadding,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            float penetrationCorrection =
                collisionRadius +
                collisionPadding -
                hit.distance;

            targetPushback = Mathf.Clamp(
                penetrationCorrection,
                0f,
                maxPushbackDistance
            );
        }

        float speed =
            targetPushback > currentPushback
                ? pushbackSpeed
                : returnSpeed;

        currentPushback = Mathf.MoveTowards(
            currentPushback,
            targetPushback,
            speed * Time.deltaTime
        );

        cameraTransform.localPosition =
            defaultLocalPosition +
            Vector3.back * currentPushback;
    }

    private void OnValidate()
    {
        collisionRadius = Mathf.Max(0.01f, collisionRadius);
        maxPushbackDistance = Mathf.Max(0f, maxPushbackDistance);
        collisionPadding = Mathf.Max(0f, collisionPadding);
        pushbackSpeed = Mathf.Max(0f, pushbackSpeed);
        returnSpeed = Mathf.Max(0f, returnSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
        {
            return;
        }

        Gizmos.DrawWireSphere(
            transform.position,
            collisionRadius
        );
    }
}