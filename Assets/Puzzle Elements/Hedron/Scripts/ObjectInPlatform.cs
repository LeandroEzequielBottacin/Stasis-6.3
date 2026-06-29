using Puzzle_Elements.IK.Scripts.SetPlayerParent;
using Puzzle_Elements.Sistema_tren.Scripts;
using UnityEngine;

namespace Puzzle_Elements.Hedron.Scripts
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody))]
    public class ObjectInPlatform : MonoBehaviour
    {
        public bool estaSubiendo;

        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayer;
        public float groundCheckRadius = 0.25f;
        public float groundCheckDistance = 0.3f;
        public float offSetY;
        public Transform groundProbe;

        [Header("Walkable Filter")]
        [Tooltip("M�nimo componente vertical de la normal para considerar 'suelo' (0=vertical, 1=plano).")]
        public float minGroundNormalY = 0.35f;

        [Header("Platform Settings")]
        public bool applyPlatformRotation = true;
        public float stickDownForce = 0.05f;
        public float maxSnapSpeed = 5f;

        [Header("Airborne Platform Hold")]
        [Tooltip("Mantener la referencia a la �ltima plataforma mientras est�s en el aire.")]
        public bool keepPlatformWhileAirborne = true;
        [Tooltip("Segundos m�ximos que se conserva _platform mientras est�s en el aire.")]
        public float airborneHoldSeconds = 2f;

        [Header("Platform Swap Coyote")]
        [Tooltip("Retraso antes de aceptar un cambio de plataforma mientras hay contacto.")]
        public float platformSwapCoyoteSeconds = 0.15f;

        [Header("Edge Coyote (bordes/cantos)")]
        [Tooltip("Ventana de gracia tras haber estado en suelo caminable para no 'despegarse' en el canto.")]
        public float edgeCoyoteSeconds = 0.12f;
        [Tooltip("Si el impacto est� dentro de esta distancia al probe, consideramos que a�n estamos pegados al borde.")]
        public float maxEdgeHitDistance = 0.12f;
        [Tooltip("Tolerancia de normal para bordes: si la normal.y es mayor a esto, permitimos forzar contacto de borde.")]
        public float edgeNormalYTolerance = 0.1f;

        private Rigidbody _rb;
        [SerializeField] private MovingPlatformDeltaPosition _platform;   // plataforma activa (puede persistir en aire)
        private Vector3 _lastHitPoint;

        // Timers/estado
        private float _airborneTimer;
        private MovingPlatformDeltaPosition _pendingPlatform;
        private float _swapTimer;
        private float _lastWalkableTime = -999f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (groundProbe == null) groundProbe = transform;
        }

        private void FixedUpdate()
        {
            // --- Ground / Platform Cast ---
            RaycastHit hit;
            Vector3 origin = groundProbe.position + Vector3.up * 0.05f + new Vector3(0f, offSetY, 0f);
            bool groundedByCast = Physics.SphereCast(
                origin, groundCheckRadius, Vector3.down,
                out hit, groundCheckDistance + 0.05f,
                groundLayer, QueryTriggerInteraction.Ignore
            );

            MovingPlatformDeltaPosition hitPlatform = null;
            Vector3 hitPoint = Vector3.zero;
            bool isWalkable = false;

            if (groundedByCast)
            {
                hitPlatform = hit.collider.GetComponentInParent<MovingPlatformDeltaPosition>();
                hitPoint = hit.point;
                isWalkable = hit.normal.y >= minGroundNormalY;
                if (isWalkable) _lastWalkableTime = Time.time;
            }

            // --- L�GICA DE CAMBIO/MANTENIMIENTO DE _platform ---
            if (groundedByCast)
            {
                // Volvimos a tocar algo: reinicio timer de aire
                _airborneTimer = 0f;

                if (isWalkable)
                {
                    // Suelo caminable
                    if (hitPlatform != null)
                    {
                        if (_platform == null)
                        {
                            // No ten�a plataforma: tomar de inmediato
                            _platform = hitPlatform;
                            _pendingPlatform = null;
                            _swapTimer = 0f;
                        }
                        else if (hitPlatform == _platform)
                        {
                            // Sigo en la misma: limpiar pendings
                            _pendingPlatform = null;
                            _swapTimer = 0f;
                        }
                        else
                        {
                            // Proponen cambio: coyote de swap
                            if (_pendingPlatform == hitPlatform)
                            {
                                _swapTimer += Time.fixedDeltaTime;
                                if (_swapTimer >= platformSwapCoyoteSeconds)
                                {
                                    _platform = _pendingPlatform;
                                    _pendingPlatform = null;
                                    _swapTimer = 0f;
                                }
                            }
                            else
                            {
                                _pendingPlatform = hitPlatform;
                                _swapTimer = 0f;
                            }
                        }

                        // Actualizar anclaje si estoy sobre la plataforma activa
                        if (hitPlatform == _platform)
                            _lastHitPoint = hitPoint;
                    }
                    else
                    {
                        // Suelo est�tico caminable: NO limpiamos _platform; solo actualizamos el �ltimo punto de apoyo
                        _lastHitPoint = hitPoint;
                        _pendingPlatform = null;
                        _swapTimer = 0f;
                    }
                }
                else
                {
                    // Superficie no caminable (canto/pared)
                    bool recentlyWalkable = (Time.time - _lastWalkableTime) <= edgeCoyoteSeconds;
                    bool nearEdge = hit.distance <= maxEdgeHitDistance;
                    bool almostWalkableNormal = hit.normal.y >= edgeNormalYTolerance;
                    bool samePlatformEdge = (hitPlatform != null && hitPlatform == _platform);

                    if (_platform != null && (samePlatformEdge || recentlyWalkable || nearEdge || almostWalkableNormal))
                    {
                        // Forzar "segu�s pegado" a efectos de anclaje/arrastre suave
                        _lastHitPoint = hitPoint;

                        // No aceptamos swaps en el canto
                        _pendingPlatform = null;
                        _swapTimer = 0f;
                    }
                    else
                    {
                        // No tocamos _platform (se mantiene por airborne hold si corresponde)
                        _pendingPlatform = null;
                        _swapTimer = 0f;
                    }
                }
            }
            else
            {
                // En el aire: mantener _platform durante airborneHoldSeconds, seg�n config
                if (keepPlatformWhileAirborne && _platform != null)
                {
                    _airborneTimer += Time.fixedDeltaTime;
                    if (_airborneTimer > airborneHoldSeconds)
                        _platform = null;
                }
                else
                {
                    _platform = null;
                }

                // Fuera de suelo: limpiar pendientes de swap
                _pendingPlatform = null;
                _swapTimer = 0f;
            }

            // --- ARRASTRE: mientras _platform != null ---
            if (_platform != null)
            {
                TrainSystem trainSystem = GetComponent<TrainSystem>();
                if(trainSystem != null)
                {
                    if (trainSystem.trainSpeed == 0) return;
                }
                Vector3 platformDeltaPos = Vector3.ClampMagnitude(
                    _platform.DeltaPosition,
                    maxSnapSpeed * Time.fixedDeltaTime
                );

                Vector3 totalDelta = platformDeltaPos;

                if (totalDelta.sqrMagnitude > 0f)
                {
                    _rb.MovePosition(_rb.position + totalDelta);
                    estaSubiendo = true;
                }
                else
                {
                    estaSubiendo = false;
                }
            }

            // Pegamento hacia abajo si hay plataforma activa y casi tocando suelo
            if (_platform != null && groundedByCast && hit.distance > 0.01f)
            {
                _rb.MovePosition(_rb.position + Vector3.down * stickDownForce);
            }
        }

        private void OnDrawGizmos()
        {
            Transform probe = groundProbe != null ? groundProbe : transform;
            Vector3 origin = probe.position + Vector3.up * 0.05f + new Vector3(0f, offSetY, 0f);
            float totalDist = groundCheckDistance + 0.05f;
            Vector3 end = origin + Vector3.down * totalDist;

            bool hit = Physics.SphereCast(
                origin,
                groundCheckRadius,
                Vector3.down,
                out RaycastHit rh,
                totalDist,
                groundLayer,
                QueryTriggerInteraction.Ignore
            );

            Gizmos.color = hit
                ? ((Application.isPlaying && _platform != null) ? Color.cyan : Color.green)
                : Color.red;

            Gizmos.DrawWireSphere(origin, groundCheckRadius);
            Gizmos.DrawWireSphere(end, groundCheckRadius);

            Vector3 right = probe.right * groundCheckRadius;
            Vector3 forward = probe.forward * groundCheckRadius;
            Gizmos.DrawLine(origin + right, end + right);
            Gizmos.DrawLine(origin - right, end - right);
            Gizmos.DrawLine(origin + forward, end + forward);
            Gizmos.DrawLine(origin - forward, end - forward);

            if (hit)
            {
                Gizmos.DrawSphere(rh.point, 0.03f);
                Gizmos.DrawLine(rh.point, rh.point + rh.normal * 0.2f);
            }

            if (Application.isPlaying && _platform != null)
            {
                Gizmos.color = Color.blue;
                Vector3 p1 = _platform.transform.position;
                Vector3 p0 = p1 - _platform.DeltaPosition;
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
}
