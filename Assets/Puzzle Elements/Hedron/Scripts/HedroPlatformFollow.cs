using Puzzle_Elements.IK.Scripts.SetPlayerParent;
using UnityEngine;

namespace Puzzle_Elements.Hedron.Scripts
{
    [RequireComponent(typeof(Rigidbody))]
    public class HedroPlatformFollower : MonoBehaviour
    {
        [Header("Detecci�n de plataforma (RB)")]
        public LayerMask platformMask = ~0;
        public Transform probe;
        public float groundCheckRadius = 0.25f;
        public float groundCheckDistance = 0.35f;

        [Header("Seguimiento")]
        public bool applyPlatformRotation = true;
        public bool rotateBodyWithPlatform;
        public float maxSnapSpeed = 8f;
        public float stickDownAccel = 15f;

        [Header("Tolerancias")]
        [Tooltip("Frames de gracia sin hit antes de soltar la plataforma.")]
        public int coyoteFrames = 3;

        private Rigidbody rb;

        private MovingPlatformDeltaPosition currentPlatform;
        private bool isOnPlatform;
        private bool skipFirstDelta;

        private Vector3 lastHitPoint;
        private int coyote;

        // debug
        [SerializeField] private bool _hitGround;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (!probe) probe = transform;

            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private void LateUpdate()
        {
            // 1) Ray/sphere para saber si ESTOY tocando algo debajo (hitGround),
            // y si ese algo pertenece (o no) a la misma plataforma que ya tengo.
            bool hitGround = ProbeGround(out var hit, out var hitTransform);

            // Si golpea algo, intentamos resolver una plataforma a partir del hit
            MovingPlatformDeltaPosition newPlatform = null;
            if (hitGround)
            {
                // �Tiene el componente en el �rbol?
                newPlatform = hitTransform.GetComponentInParent<MovingPlatformDeltaPosition>();

                // Si NO lo tiene pero ya tenemos una plataforma y el collider es hijo de ella, mantenemos la misma
                if (!newPlatform && currentPlatform &&
                    (hitTransform == currentPlatform.transform || hitTransform.IsChildOf(currentPlatform.transform)))
                {
                    newPlatform = currentPlatform;
                }
            }

            // 2) Cambio/instancia de plataforma
            if (newPlatform != currentPlatform)
            {
                currentPlatform = newPlatform;
                isOnPlatform = (currentPlatform != null);
                skipFirstDelta = isOnPlatform;

                if (isOnPlatform)
                {
                    // reset de inercia para evitar tirones
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            // Actualizamos punto de contacto si hubo hit
            if (hitGround) lastHitPoint = hit.point;

            // 3) Coyote time: si no hubo hitGround este frame pero est�bamos en plataforma, aguantamos unos frames
            if (hitGround)
            {
                coyote = coyoteFrames;
            }
            else if (isOnPlatform && coyote > 0)
            {
                coyote--;
                hitGround = true; // mantenemos consideraci�n de contacto durante la gracia
            }
            else if (coyote <= 0 && !hitGround)
            {
                isOnPlatform = false; // soltamos
                currentPlatform = null;
            }

            // 4) Aplicar delta de la plataforma
            if (isOnPlatform && currentPlatform != null)
            {
                Vector3 deltaPos = currentPlatform.DeltaPosition;        // Deben venir de FixedUpdate en la plataforma
                Quaternion deltaRot = currentPlatform.DeltaRotation;

                Vector3 rotationDelta = Vector3.zero;
                if (applyPlatformRotation)
                {
                    Vector3 rotated = deltaRot * (rb.position - lastHitPoint) + lastHitPoint;
                    rotationDelta = rotated - rb.position;
                }

                Vector3 totalDelta = deltaPos + rotationDelta;

                // Clamp por seguridad
                float maxStep = maxSnapSpeed * Time.fixedDeltaTime;
                if (totalDelta.magnitude > maxStep)
                    totalDelta = totalDelta.normalized * maxStep;

                if (!skipFirstDelta && totalDelta.sqrMagnitude > 0f)
                    rb.MovePosition(rb.position + totalDelta);

                if (rotateBodyWithPlatform)
                    rb.MoveRotation(deltaRot * rb.rotation);

                // Pegamento hacia abajo mientras hay contacto (real o de gracia)
                if (hitGround && stickDownAccel > 0f)
                    rb.AddForce(Vector3.down * stickDownAccel, ForceMode.Acceleration);
            }

            skipFirstDelta = false;

            // debug
            _hitGround = hitGround;
        }

        private bool ProbeGround(out RaycastHit hit, out Transform hitTransform)
        {
            Vector3 origin = probe.position + Vector3.up * 0.05f;
            float dist = groundCheckDistance + 0.05f;

            bool gotHit = Physics.SphereCast(
                origin,
                groundCheckRadius,
                Vector3.down,
                out hit,
                dist,
                platformMask,
                QueryTriggerInteraction.Ignore
            );

            hitTransform = gotHit ? hit.collider.transform : null;

            // IMPORTANTE: "grounded" significa SOLO que tocamos algo bajo nosotros,
            // NO que ese algo tenga el componente de plataforma.
            return gotHit;
        }

        private void OnDrawGizmosSelected()
        {
            Transform p = probe ? probe : transform;
            Vector3 origin = p.position + Vector3.up * 0.05f;
            Vector3 end = origin + Vector3.down * (groundCheckDistance + 0.05f);

            Gizmos.color = (Application.isPlaying && isOnPlatform) ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(origin, groundCheckRadius);
            Gizmos.DrawWireSphere(end, groundCheckRadius);

            Vector3 r = p.right * groundCheckRadius;
            Vector3 f = p.forward * groundCheckRadius;
            Gizmos.DrawLine(origin + r, end + r);
            Gizmos.DrawLine(origin - r, end - r);
            Gizmos.DrawLine(origin + f, end + f);
            Gizmos.DrawLine(origin - f, end - f);
        }
    }
}
