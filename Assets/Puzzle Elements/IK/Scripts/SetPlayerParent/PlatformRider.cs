using UnityEngine;

namespace Puzzle_Elements.IK.Scripts.SetPlayerParent
{
    [RequireComponent(typeof(CharacterController))]
    public class PlatformRider : MonoBehaviour
    {
        [Header("Detecci�n de plataforma")]
        public LayerMask groundMask;            // capa de �suelo / plataforma�
        public float groundCheckRadius = 0.25f; // radio de chequeo
        public float groundCheckDistance = 0.3f;// distancia hacia abajo
        public Transform groundProbe;           // punto de origen (ej: pies del player)

        [Header("Ajustes")]
        public bool applyPlatformRotation = true;
        public float stickDownForce = 0.05f;    // empuje m�nimo hacia abajo para no �levitar�
        public float maxSnapSpeed = 5f;         // l�mite para no pegar saltos con plataformas muy r�pidas

        private CharacterController _cc;
        [SerializeField]private MovingPlatformDeltaPosition _platform;       // plataforma actual (si hay)
        private Vector3 _lastHitPoint;          // punto de contacto del frame anterior
        [SerializeField]private bool _isOnPlatform;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (groundProbe == null) groundProbe = transform;
            // Recomiendo:
            // _cc.minMoveDistance = 0f;
        }

        private void Update()
        {
            // 1) Detectar si estamos sobre una plataforma v�lida
            RaycastHit hit;
            Vector3 origin = groundProbe.position + Vector3.up * 0.05f;
            bool groundedByCast = Physics.SphereCast(
                origin, groundCheckRadius, Vector3.down,
                out hit, groundCheckDistance + 0.05f, groundMask, QueryTriggerInteraction.Ignore);

            MovingPlatformDeltaPosition newPlatform = null;
            Vector3 hitPoint = Vector3.zero;

            if (groundedByCast)
            {
                newPlatform = hit.collider.GetComponentInParent<MovingPlatformDeltaPosition>();
                hitPoint = hit.point;
            }

            // 2) Si cambiamos de plataforma (o entramos/salimos), actualizar estado
            if (newPlatform != _platform)
            {
                _platform = newPlatform;
                _isOnPlatform = (_platform != null);
                _lastHitPoint = hitPoint;
            }

            // 3) Si estamos sobre plataforma, aplicar su delta (traslaci�n + rotaci�n)
            if (_isOnPlatform && _platform != null)
            {
                Vector3 platformDeltaPos = Vector3.ClampMagnitude(_platform.DeltaPosition, maxSnapSpeed * Time.deltaTime);

                // Rotaci�n alrededor del punto de contacto anterior (para que no �deslice� al rotar)
                Vector3 rotatedPos = transform.position;
                if (applyPlatformRotation)
                {
                    rotatedPos = _platform.DeltaRotation * (transform.position - _lastHitPoint) + _lastHitPoint;
                }
                Vector3 rotationDelta = rotatedPos - transform.position;

                // Delta total a aplicar al player
                Vector3 totalDelta = platformDeltaPos + rotationDelta;

                if (totalDelta.sqrMagnitude > 0f)
                    _cc.Move(totalDelta);
            }

            // 4) Peque�o �pegamento� hacia abajo para mantener contacto
            if (_isOnPlatform && _cc.isGrounded)
            {
                _cc.Move(Vector3.down * stickDownForce);
            }

            _lastHitPoint = hitPoint;
        }
        private void OnDrawGizmos()
        {
            // Origen del chequeo
            Transform probe = groundProbe != null ? groundProbe : transform;
            Vector3 origin = probe.position + Vector3.up * 0.05f;
            float totalDist = groundCheckDistance + 0.05f;
            Vector3 end = origin + Vector3.down * totalDist;

            // Hacemos el mismo SphereCast que usa el script
            bool hit = Physics.SphereCast(
                origin,
                groundCheckRadius,
                Vector3.down,
                out RaycastHit rh,
                totalDist,
                groundMask,
                QueryTriggerInteraction.Ignore
            );

            // Color seg�n estado
            Gizmos.color = hit
                ? ((Application.isPlaying && _platform != null) ? Color.cyan : Color.green)
                : Color.red;

            // Dibujo "c�psula" (aproximaci�n con dos esferas y 4 l�neas)
            Gizmos.DrawWireSphere(origin, groundCheckRadius);
            Gizmos.DrawWireSphere(end, groundCheckRadius);

            Vector3 right = probe.right * groundCheckRadius;
            Vector3 forward = probe.forward * groundCheckRadius;
            Gizmos.DrawLine(origin + right, end + right);
            Gizmos.DrawLine(origin - right, end - right);
            Gizmos.DrawLine(origin + forward, end + forward);
            Gizmos.DrawLine(origin - forward, end - forward);

            // Punto de impacto y normal (si hay)
            if (hit)
            {
                Gizmos.DrawSphere(rh.point, 0.03f);
                Gizmos.DrawLine(rh.point, rh.point + rh.normal * 0.2f);
            }

            // Visual del delta de la plataforma (solo en Play y si hay plataforma)
            if (Application.isPlaying && _platform != null)
            {
                Gizmos.color = Color.blue;
                Vector3 p1 = _platform.transform.position;
                Vector3 p0 = p1 - _platform.DeltaPosition;
                Gizmos.DrawLine(p0, p1); // l�nea que indica cu�nto se movi� este frame
            }
        }

    }
}
