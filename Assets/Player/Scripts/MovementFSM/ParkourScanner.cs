using System;
using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Player.Scripts.MovementFSM
{
    public enum ParkourAction
    {
        None,
        Vault,
        Climb,
        WallrunLeft,
        WallrunRight
    }

    [Serializable]
    public struct ParkourProbe
    {
        public ParkourAction action;

        public float playerRadius;
        public float playerHeight;

        // Datos comunes
        public Vector3 hitPoint;
        public Vector3 hitNormal;
        public float obstacleHeight;

        // VAULT
        public Vector3 vaultTopPoint;
        public Vector3 vaultLandPoint;
        public float vaultDistance;
        public Vector3 vaultStartPoint;
        public Vector3 vaultMidXZ;
        public float vaultArcApex;
        public Collider vaultObstacle;
        public Vector3 vaultForward;
        public bool vaultLandOnSameCollider;
        
        public Vector3 climbLedgePoint;
        public Vector3 climbStandPoint;
        public float climbHeight;

        public Vector3 wallRunWallPoint;
        public Vector3 wallRunNormal;
        public int wallSide;
        public Collider wallRunCollider;

        public static ParkourProbe None => new ParkourProbe { action = ParkourAction.None };
    }

    namespace Player.Scripts.MovementFSM
    {
        [DefaultExecutionOrder(50)]
        public class ParkourScanner : MonoBehaviour
        {
            [Header("References")] public Rigidbody rb;
            public Transform cameraHolder;

            [Header("Layers")] public LayerMask environmentMask;
            public LayerMask groundMask;

            [Header("Parkour Tags")] public string tagVault = "Vault";
            public string tagClimb = "Climb";
            public string tagWallrun = "Wallrun";

            [Header("General")] [Tooltip("Distancia máx. al obstáculo para iniciar (m).")]
            public float forwardCheckDistance = 1.2f;

            [Tooltip("Separación mínima a paredes/techos para considerar 'libre' (m).")]
            public float clearanceSkin = 0.06f;

            [Header("Grounding (Scanner)")] public CapsuleCollider capsule;
            public float groundCheckDistance = 0.2f;
            [Range(0f, 80f)] public float maxGroundSlopeDeg = 55f;

            [Header("Grounding (Masks)")]
            [Tooltip("Si true, también considera environmentMask como 'caminar' (filtra por pendiente).")]
            public bool useEnvAsGround = true;

            [Header("Ground Notify (extra)")]
            [Tooltip("Si true, re-notifica OnGroundedChanged(true, hit) estando en suelo.")]
            public bool continuousGroundNotify = true;

            [Tooltip("0 = notifica cada frame; >0 = cada N segundos.")]
            public float groundNotifyInterval; // por defecto: cada frame

            [Tooltip("Re-notifica si el punto de apoyo cambia más de este umbral (m).")]
            public float groundReNotifyPosEps = 0.01f;

            [Tooltip("Re-notifica si la normal cambia más que este cos(ángulo). 0.999≈2.6°")] [Range(-1f, 1f)]
            public float groundReNotifyNormalCos = 0.999f;

            float _nextGroundNotifyTime;
            RaycastHit _lastNotifiedGroundHit;
            bool _hasNotifiedGroundOnce;


            LayerMask WalkableGroundMask => useEnvAsGround ? (groundMask | environmentMask) : groundMask;

            private bool Grounded { get; set; }
            private RaycastHit GroundHit { get; set; }
            private float GroundSlopeDeg { get; set; }

            public bool IsGrounded() => Grounded;

            [Header("Vault")] public float vaultMinHeight = 0.4f;
            public float vaultMaxHeight = 1.2f;
            public float vaultTopClearance = 0.25f;
            public float vaultMinForward = 0.4f;
            public float vaultMaxForward = 2.2f;
            public float vaultDownCast = 2.5f;

            [Header("Climb")] public float climbDetectLength = 0.9f;
            public float climbSphereRadius = 0.35f;
            [Range(0f, 90f)] public float climbMaxLookAngle = 65f;
            [Range(0f, 89f)] public float climbMinWallSlopeDeg = 70f;

            [Header("Wallrun")] public float wallCheckDistance = 0.9f;
            public float wallMinHeight = 1.4f;
            [Range(0f, 60f)] public float wallMaxSlopeDeg = 15f;
            [Range(0f, 70f)] public float wallToForwardMaxAngle = 55f;
            public float wallMinSpeed = 3.5f;

            [Tooltip("m/s hacia la pared para permitir entrada aunque el ángulo/tangente fallen")]
            public float wallApproachMinInto = 2.0f;

            [Tooltip("Ventana para mantener el último hit al cruzar un seam")]
            public float wallSeamStickTime = 0.15f;

            [Tooltip("Gap máximo al plano anterior para tratarlo como continuidad")]
            public float wallSeamMaxGap = 0.12f;

            [Tooltip("Relaja ángulo máx.  si venís en carrera hacia la pared")]
            public float wallApproachAngleBonus = 25f;

            [Tooltip("Tiempo para permitir reenganchar OTRA pared aún en cooldown.")]
            public float wallCrossRegrabGrace = 0.15f;

            [Tooltip("Factor para relajar velocidad mínima durante la gracia.")]
            public float wallGraceMinSpeedMul = 0.6f;

            [Tooltip("Bonus angular durante la gracia (grados).")]
            public float wallGraceAngleBonus = 10f;

            [Tooltip("Fallback frontal para entrar cuando saltas directo a la pared.")]
            public float wallFrontAcquireDistance = 1.1f;

            [Tooltip("Radio del SphereCast lateral (en múltiplos del Radius).")]
            public float wallSideSphereRadiusMul = 0.8f;

            [Tooltip("Empuje extra fuera de la pared para el check de cabeza.")]
            public float wallHeadClearPush = 0.12f;

            [Tooltip("Ancho mínimo de la superficie para ser considerada 'wallrunnable' (m). 0 para desactivar.")]
            public float wallMinWidth = 0.4f;

            [Tooltip("Distancia para el re-check de la anchura (m).")]
            public float wallWidthCheckDist = 0.15f;

            private RaycastHit _lastWallHit;
            private float _lastWallTime = -999f;

            [Header("Ground")] public bool requireAirForWallrun = true;

            [Header("Debug")] public bool drawGizmos = true;

            public ParkourProbe Probe { get; private set; }
            public event Action<ParkourProbe> OnProbeUpdated = delegate { };
            public event Action<bool, RaycastHit> OnGroundedChanged = delegate { };

            private bool _prevGrounded;
            private RaycastHit _prevGroundHit;

            float Radius => capsule ? Mathf.Max(0.05f, capsule.radius) : 0.3f;
            float Height => capsule ? capsule.height : 1.8f;

            public RaycastHit CurrentGroundHit => GroundHit;
            public float CurrentGroundSlopeDeg => GroundSlopeDeg;

            void Reset()
            {
                rb = GetComponent<Rigidbody>();
                capsule = GetComponent<CapsuleCollider>();
            }

            void Update()
            {
                UpdateGrounding();

                if (Grounded != _prevGrounded)
                {
                    _prevGrounded = Grounded;

                    if (Grounded)
                    {
                        _hasNotifiedGroundOnce = true;
                        _lastNotifiedGroundHit = GroundHit;
                        _nextGroundNotifyTime = Time.time + groundNotifyInterval;
                    }
                    else
                    {
                        _hasNotifiedGroundOnce = false;
                    }
                }
                else if (Grounded && continuousGroundNotify)
                {
                    bool timeOk = (groundNotifyInterval <= 0f) || (Time.time >= _nextGroundNotifyTime);

                    bool hitDiff =
                        !_hasNotifiedGroundOnce ||
                        GroundHit.collider != _lastNotifiedGroundHit.collider ||
                        (GroundHit.point - _lastNotifiedGroundHit.point).sqrMagnitude >
                        (groundReNotifyPosEps * groundReNotifyPosEps) ||
                        Vector3.Dot(GroundHit.normal.normalized, _lastNotifiedGroundHit.normal.normalized) <
                        groundReNotifyNormalCos;

                    if (timeOk || hitDiff)
                    {
                        OnGroundedChanged(true, GroundHit);
                        _lastNotifiedGroundHit = GroundHit;
                        _hasNotifiedGroundOnce = true;
                        if (groundNotifyInterval > 0f)
                            _nextGroundNotifyTime = Time.time + groundNotifyInterval;
                    }
                }

                Probe = Evaluate();
                OnProbeUpdated(Probe);
            }

            ParkourProbe Evaluate()
            {
                if (!capsule || !cameraHolder)
                {
                    Debug.LogWarning($"[Scanner] Falta referencia: capsule={capsule}, cameraHolder={cameraHolder}");
                    return ParkourProbe.None;
                }

                if (TryDetectVault(out var vault))
                {
                    return vault;
                }

                if (TryDetectClimb(out var climb))
                {
                    return climb;
                }

                if (TryDetectWallrun(+1, out var wrRight))
                {
                    return wrRight;
                }

                if (TryDetectWallrun(-1, out var wrLeft))
                {
                    return wrLeft;
                }

                return ParkourProbe.None;
            }

            #region GROUNDING

            [Header("Grounding (Tuning)")] [Tooltip("Distancia máx. a la superficie para 'pegar' al suelo (m).")]
            public float groundSnapDistance = 0.08f;

            [Tooltip("Aumenta el alcance del cast según la caída.")]
            public float fallProbeVelocityMul = 0.035f;

            private float _lastGroundTrueTime;

            void GetCapsuleWorld(out Vector3 bottom, out float r)
            {
                const float skin = 0.02f;
                r = Mathf.Max(0.01f, (capsule ? capsule.radius : 0.3f) - skin);
                Vector3 center = transform.TransformPoint(capsule ? capsule.center : Vector3.zero);
                float height = capsule ? capsule.height : 1.8f;
                float half = height * 0.5f - r;
                bottom = center - Vector3.up * half;
            }

            bool IsValidGroundHit(in RaycastHit hit)
            {
                if (!hit.collider) return false;

                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > maxGroundSlopeDeg)
                {
                    //Debug.Log($"[Grounding] Hit {hit.collider.name}, pero RECHAZADO: Pendiente ({slope:F1}°) > Máxima ({maxGroundSlopeDeg}°).", hit.collider);
                    return false;
                }

                float feetY = transform.position.y + (capsule ? capsule.center.y - capsule.height * 0.5f : 0f);
                if (hit.point.y > feetY + 0.1f) return false;
            
                return true;
            }

            void UpdateGrounding()
            {
                if (!capsule)
                {
                    Grounded = false;
                    if (Grounded == _prevGrounded) return;
                    OnGroundedChanged(Grounded, default);
                    _prevGrounded = Grounded;
                    return;
                }

                GetCapsuleWorld(out var bottom, out var r);
                Vector3 castOrigin = bottom + Vector3.up * r;

                float extraByFall = (rb && rb.linearVelocity.y < 0f) 
                    ? Mathf.Abs(rb.linearVelocity.y * fallProbeVelocityMul) 
                    : 0f;
                float castDistance = groundCheckDistance + extraByFall;
    
                bool hitDetected = Physics.SphereCast(
                    castOrigin,
                    r,
                    Vector3.down,
                    out RaycastHit hitInfo,
                    castDistance,
                    WalkableGroundMask,
                    QueryTriggerInteraction.Ignore
                );
                
                if (hitDetected && IsValidGroundHit(hitInfo))
                {
                    Grounded = true;
                    GroundHit = hitInfo;
                    GroundSlopeDeg = Vector3.Angle(hitInfo.normal, Vector3.up);
                }
                else
                {
                    Grounded = false;
                    GroundHit = default;
                    GroundSlopeDeg = 0f;
                }
                
                if (Grounded != _prevGrounded)
                {
                    //Debug.Log($"[Grounding] *** ESTADO CAMBIÓ *** -> {Grounded}. Hit: {(GroundHit.collider ? GroundHit.collider.name : "NADA")}", this);
                    
                    OnGroundedChanged(Grounded, GroundHit); 
        
                    _prevGrounded = Grounded;

                    if (Grounded)
                    {
                        _hasNotifiedGroundOnce = true;
                        _lastNotifiedGroundHit = GroundHit;
                        _nextGroundNotifyTime = Time.time + groundNotifyInterval;
                    }
                    else
                    {
                        _hasNotifiedGroundOnce = false;
                    }
                }
                else if (Grounded && continuousGroundNotify)
                {
                    bool timeOk = (groundNotifyInterval <= 0f) || (Time.time >= _nextGroundNotifyTime);

                    bool hitDiff =
                        !_hasNotifiedGroundOnce ||
                        GroundHit.collider != _lastNotifiedGroundHit.collider ||
                        (GroundHit.point - _lastNotifiedGroundHit.point).sqrMagnitude >
                        (groundReNotifyPosEps * groundReNotifyPosEps) ||
                        Vector3.Dot(GroundHit.normal.normalized, _lastNotifiedGroundHit.normal.normalized) <
                        groundReNotifyNormalCos;

                    if (timeOk || hitDiff)
                    {
                        OnGroundedChanged(true, GroundHit);
                        _lastNotifiedGroundHit = GroundHit;
                        _hasNotifiedGroundOnce = true;
                        if (groundNotifyInterval > 0f)
                            _nextGroundNotifyTime = Time.time + groundNotifyInterval;
                    }
                }
            }

            #endregion

            #region VAULT

            bool TryDetectVault(out ParkourProbe result)
            {
                result = ParkourProbe.None;
                var m = GetComponent<Model>();
                bool runHeld = m && m.runningKeyPressed;
                if (!runHeld)
                {
                    return false;
                }

                if (m && Time.time < m.blockVaultUntil)
                {
                    return false;
                }

                float r = Radius;
                float h = Height;

                Vector3 pos = transform.position;
                Vector3 fwd = GetPlanarForward();
                float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);
                Vector3 chestOrigin = pos + Vector3.up * chest;

                if (!Physics.Raycast(chestOrigin, fwd, out var hitFront, forwardCheckDistance, environmentMask,
                        QueryTriggerInteraction.Ignore))
                    return false;

                if (!hitFront.collider || !hitFront.collider.CompareTag(tagVault))
                {
                    return false;
                }

                if (!TopFromHit(hitFront, out float topY))
                {
                    return false;
                }

                float feetY = (pos + Vector3.up * r).y;
                float obsHeight = topY - feetY;
                if (obsHeight < vaultMinHeight || obsHeight > vaultMaxHeight) return false;

                Vector3 topProbeStart = new Vector3(hitFront.point.x, topY + 0.02f, hitFront.point.z);
                if (!Physics.Raycast(topProbeStart, Vector3.down, out var topHit, 1f, environmentMask | groundMask,
                        QueryTriggerInteraction.Ignore))
                {
                    return false;
                }

                Vector3 topPoint = topHit.point + Vector3.up * clearanceSkin;
                Vector3 midXZ = new Vector3(topPoint.x, topPoint.y, topPoint.z) + fwd * Mathf.Max(r * 0.6f, 0.2f);
                midXZ.y = topPoint.y;
                
                // --- INICIO DE CORRECCIÓN 1: Bug de 'vaultTopClearance' ---
                // Tu código original lo comprobaba pero no hacía 'return'.
                // Lo movemos aquí: si no hay espacio arriba, no hay vault.
                Vector3 topClearanceCheckOrigin = topPoint + Vector3.up * 0.01f;
                if (Physics.Raycast(topClearanceCheckOrigin, Vector3.up, vaultTopClearance, environmentMask, QueryTriggerInteraction.Ignore))
                {
                    if (drawGizmos) Debug.DrawLine(topClearanceCheckOrigin, topClearanceCheckOrigin + Vector3.up * vaultTopClearance, Color.red, 0.1f);
                    return false; // No hay espacio, no se puede saltar
                }
                if (drawGizmos) Debug.DrawLine(topClearanceCheckOrigin, topClearanceCheckOrigin + Vector3.up * vaultTopClearance, Color.green, 0.1f);
                // --- FIN DE CORRECCIÓN 1 ---

                float minF = Mathf.Max(vaultMinForward, r * 1.2f);
                float maxF = Mathf.Max(minF + 0.3f, vaultMaxForward);
                LayerMask maskTop = environmentMask | groundMask;

                Vector3 land = Vector3.zero;
                bool foundLand = false;
                bool foundSameTop = false;

                Vector3 capsuleCenterOffset = (capsule ? capsule.center : Vector3.up * (h / 2f));

                for (float f = minF; f <= maxF + 0.0001f; f += 0.1f)
                {
                    Vector3 over = topPoint + fwd * f + Vector3.up * 0.05f;
                    if (Physics.Raycast(over, Vector3.down, out var downHit, vaultDownCast, maskTop,
                            QueryTriggerInteraction.Ignore))
                    {
                        if (downHit.collider != hitFront.collider)
                        {
                            Vector3 stand = downHit.point + capsuleCenterOffset;
                            if (HasClearanceCapsule(stand, h - r * 2f))
                            {
                                land = downHit.point;
                                foundLand = true;
                                break;
                            }
                        }
                    }
                }

                if (!foundLand)
                {
                    for (float f = 0.05f; f <= minF + 0.0001f; f += 0.1f) 
                    {
                        Vector3 over = topPoint + fwd * f + Vector3.up * 0.05f;
                        if (Physics.Raycast(over, Vector3.down, out var downHit, vaultDownCast, maskTop,
                                QueryTriggerInteraction.Ignore))
                        {
                            if (downHit.collider == hitFront.collider)
                            {
                                Vector3 stand = downHit.point + capsuleCenterOffset;
                                if (HasClearanceCapsule(stand, h - r * 2f))
                                {
                                    land = downHit.point;
                                    foundLand = true;
                                    foundSameTop = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!foundLand)
                {
                    Vector3 overMax = topPoint + fwd * (maxF + 0.05f) + Vector3.up * 0.2f;
                    if (Physics.Raycast(overMax, Vector3.down, out var dTopMax, vaultDownCast, environmentMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        if (dTopMax.collider == hitFront.collider)
                        {
                            land = topPoint;
                            foundLand = true;
                            foundSameTop = true;
                        }
                    }
                }
                
                if (!foundLand) return false;

                var start = rb ? rb.position : transform.position;
                Vector3 vaultFwd = GetPlanarForward();
                if (vaultFwd.sqrMagnitude > 0f) vaultFwd.Normalize();

                Vector3 desired = land - start;
                desired.y = 0f;
                if (desired.sqrMagnitude > 1e-6f)
                {
                    desired.Normalize();
                    Vector3 vf = vaultFwd;
                    vf.y = 0f;
                    if (vf.sqrMagnitude > 1e-6f && Vector3.Dot(vf.normalized, desired) < 0f)
                        vaultFwd = -vaultFwd;
                }

                result = new ParkourProbe
                {
                    action = ParkourAction.Vault,
                    hitPoint = hitFront.point,
                    hitNormal = hitFront.normal,
                    obstacleHeight = obsHeight,
                    vaultTopPoint = topPoint,
                    vaultMidXZ = midXZ,
                    vaultLandPoint = land,
                    vaultDistance =
                        Vector3.Distance(new Vector3(topPoint.x, 0, topPoint.z), new Vector3(land.x, 0, land.z)),
                    vaultStartPoint = start,
                    playerRadius = r,
                    playerHeight = h,
                    vaultObstacle = hitFront.collider,
                    vaultForward = vaultFwd,
                    vaultLandOnSameCollider = foundSameTop
                };
                return true;
            }

            #endregion

            #region CLIMB

            // ReSharper disable Unity.PerformanceAnalysis
            bool TryDetectClimb(out ParkourProbe result)
            {
                result = ParkourProbe.None;

                var m = GetComponent<Model>();
                if (m && Time.time < m.blockClimbUntil) return false;

                // 0) Orígenes básicos
                float h = Height;
                float r = Radius;
                float chest = Mathf.Clamp(h * 0.55f, 0.8f, 1.1f);
                Vector3 pos = transform.position;
                Vector3 origin = pos + Vector3.up * chest;
                Vector3 fwd = GetPlanarForward();

                // 1) SphereCast frontal para pared
                if (!Physics.SphereCast(origin, climbSphereRadius, fwd, out RaycastHit rawHit,
                        climbDetectLength, environmentMask, QueryTriggerInteraction.Collide))
                    return false;

                // 2) Refinar normal verdadera
                RaycastHit hit = rawHit;
                Vector3 toWall = (rawHit.point - origin);
                if (toWall.sqrMagnitude > 1e-6f)
                {
                    toWall.Normalize();
                    if (Physics.Raycast(origin, toWall, out RaycastHit refine, rawHit.distance + 0.05f,
                            environmentMask, QueryTriggerInteraction.Collide))
                        hit = refine;
                }

                if (!hit.collider) return false;

                // 3) Tag + pendiente ≈ pared + ángulo de mirada
                if (!HasClimbTag(hit.collider)) return false;

                float slopeDeg = Vector3.Angle(hit.normal, Vector3.up);
                if (slopeDeg < climbMinWallSlopeDeg) return false;

                float lookAng = Vector3.Angle(fwd, -hit.normal);
                if (lookAng > climbMaxLookAngle) return false;

                // 4) HEAD-CLEAR: si a nivel cabeza NO hay pared → estamos en “borde”
                float headY = Mathf.Clamp(h * 0.92f, 1.3f, 1.9f);
                Vector3 headOrigin = pos + Vector3.up * headY;
                bool headHits = Physics.SphereCast(headOrigin, r * 0.95f, fwd, out _, climbDetectLength * 0.9f,
                    environmentMask, QueryTriggerInteraction.Ignore);

                Vector3 climbLedge = Vector3.zero;
                Vector3 climbStand = Vector3.zero;
                float climbHeight = 0f;

                if (!headHits)
                {
                    // 5) DOWNCAST a la tapa desde un poco adelante y arriba
                    Vector3 inward = (-hit.normal).normalized; // “hacia adentro” de la tapa
                    Vector3 downFrom = headOrigin + inward * (r + clearanceSkin + 0.10f) + Vector3.up * 0.40f;

                    if (Physics.Raycast(downFrom, Vector3.down, out RaycastHit topHit, 2.2f,
                            environmentMask | groundMask, QueryTriggerInteraction.Ignore))
                    {
                        // Debe ser “pisable”
                        float upDot = Vector3.Dot(topHit.normal.normalized, Vector3.up);
                        if (upDot >= Mathf.Cos(32f * Mathf.Deg2Rad)) // ≈ <=32° de inclinación
                        {
                            climbLedge = topHit.point;

                            // 6) Punto de stand ligeramente adentro y elevado al radio
                            Vector3 stand = topHit.point + inward * (r + clearanceSkin) +
                                            Vector3.up * (r + clearanceSkin);

                            // 7) Clearance real de la cápsula en destino
                            if (HasClearanceCapsule(stand, h - 2f * r))
                            {
                                climbStand = stand;
                                float feetY = (pos + Vector3.up * r).y;
                                climbHeight = topHit.point.y - feetY;
                            }
                        }
                    }
                }

                // 8) Resultado (aunque no haya ledge, reportamos pared escalable)
                result = new ParkourProbe
                {
                    action = ParkourAction.Climb,
                    hitPoint = hit.point,
                    hitNormal = hit.normal,
                    playerHeight = h,
                    playerRadius = r,

                    climbLedgePoint = climbLedge,
                    climbStandPoint = climbStand,
                    climbHeight = climbHeight
                };

                return true;
            }

            bool HasClimbTag(Collider c)
            {
                if (!c) return false;
                if (c.CompareTag(tagClimb)) return true;

                var t = c.transform;
                while (t)
                {
                    if (t.CompareTag(tagClimb)) return true;
                    t = t.parent;
                }

                return false;
            }

            #endregion

            #region WALLRUN

            // ReSharper disable Unity.PerformanceAnalysis
            bool TryDetectWallrun(int side, out ParkourProbe result)
            {
                result = ParkourProbe.None;
                var m = GetComponent<Model>();

                if (m && Time.time < m.wallJustJumpedUntil)
                {
                    result = ParkourProbe.None;
                    return false;
                }

                // 0) Heading y lados
                Vector3 heading = GetHeadingPlanar();
                if (heading.sqrMagnitude < 1e-6f) return false;
                Vector3 rightHeading = Vector3.Cross(Vector3.up, heading).normalized;
                Vector3 sideDir = (side < 0 ? -rightHeading : rightHeading);

                float mid = Mathf.Clamp(Height * 0.5f, 0.8f, 1.0f);
                Vector3 origin = transform.position + Vector3.up * mid;
                float radius = Mathf.Max(0.08f, Radius * wallSideSphereRadiusMul);

                // 1) Lateral + fallback frontal
                bool got = Physics.SphereCast(origin, radius, sideDir, out var hit, wallCheckDistance,
                    environmentMask, QueryTriggerInteraction.Ignore);

                if (!got)
                    got = Physics.Raycast(origin, heading, out hit, wallFrontAcquireDistance,
                        environmentMask, QueryTriggerInteraction.Ignore);

                if (!got && (Time.time - _lastWallTime) <= wallSeamStickTime && _lastWallHit.collider)
                {
                    if (!(m && Time.time < m.wallSeamDisableUntil))
                    {
                        float distToPlane = Mathf.Abs(Vector3.Dot((transform.position - _lastWallHit.point),
                            _lastWallHit.normal));
                        if (distToPlane <= wallSeamMaxGap)
                        {
                            hit = _lastWallHit;
                            got = true;
                        }
                    }
                }

                if (!got) return false;

                // 2) Refinar normal verdadera
                Vector3 toWall = (hit.point - origin);
                if (toWall.sqrMagnitude > 1e-6f)
                {
                    toWall.Normalize();
                    if (Physics.Raycast(origin, toWall, out RaycastHit refine, hit.distance + 0.05f,
                            environmentMask, QueryTriggerInteraction.Ignore))
                        hit = refine;
                }

                if (!hit.collider) return false;

                if (!HasMinimumWallWidth(hit))
                {
                    if (drawGizmos) Debug.DrawLine(hit.point, hit.point + Vector3.up * 1.5f, Color.red, 0.1f);
                    return false;
                }

                if (!hit.collider.CompareTag(tagWallrun)) return false;

                // 3) Validar casi vertical
                float upDot = Mathf.Abs(Vector3.Dot(hit.normal.normalized, Vector3.up));
                if (upDot > Mathf.Sin(wallMaxSlopeDeg * Mathf.Deg2Rad)) return false;

                // 4) Cooldown inteligente: permití re-grab si venís entrando fuerte
                bool cooldownActive = (m && Time.time < m.blockWallrunUntil);
                bool forceAllow = false;
                if (!(m && Time.time < m.wallJustJumpedUntil)) // <-- NO permitir override si recién saltó
                {
                    float speedInto = rb ? Vector3.Dot(rb.linearVelocity, -hit.normal) : 0f;
                    forceAllow = speedInto >= wallApproachMinInto
                                 || ((Time.time - m.lastWallDetachTime) <= wallCrossRegrabGrace);
                }

                if (cooldownActive && m)
                {
                    bool sameCol = m.lastWallCollider && hit.collider == m.lastWallCollider;
                    bool samePlane = (m.lastWallNormal != Vector3.zero &&
                                      Vector3.Dot(hit.normal.normalized, m.lastWallNormal.normalized) > 0.96f);
                    if ((sameCol || samePlane) && !forceAllow) return false;
                }

                // 5) Tangente de pared y tests de entrada
                Vector3 wallForward = Vector3.Cross(hit.normal, Vector3.up).normalized;
                if (Vector3.Dot(heading, wallForward) < 0f) wallForward = -wallForward;

                bool inGrace = (m && ((Time.time - m.lastWallDetachTime) <= wallCrossRegrabGrace));
                float angMax = wallToForwardMaxAngle + (inGrace ? wallGraceAngleBonus : 0f);

                Vector3 horizVel = rb ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z) : Vector3.zero;
                float tangentialSpeed = Mathf.Abs(Vector3.Dot(horizVel, wallForward));
                float minSpeed = wallMinSpeed * (inGrace ? wallGraceMinSpeedMul : 1f);

                float speedInto2 = rb ? Vector3.Dot(rb.linearVelocity, -hit.normal) : 0f;
                bool approachOk = speedInto2 >= wallApproachMinInto && Vector3.Dot(heading, -hit.normal) > 0.1f;

                if (Vector3.Angle(heading, wallForward) > (angMax + (approachOk ? wallApproachAngleBonus : 0f)) &&
                    !approachOk) return false;

                if (tangentialSpeed < minSpeed && !approachOk) return false;

                // 6) Clearance cabeza
                Vector3 head = origin + Vector3.up * wallMinHeight +
                               hit.normal * (Radius + clearanceSkin + wallHeadClearPush);
                if (!HasClearanceCapsule(head, Radius * 2f)) return false;

                // 7) Resolver lado
                int resolvedSide = (Vector3.Dot(hit.normal, rightHeading) < 0f) ? -1 : +1;

                // 8) Guardar último buen hit para seam
                _lastWallHit = hit;
                _lastWallTime = Time.time;

                // 9) Probe
                result = new ParkourProbe
                {
                    action = resolvedSide > 0 ? ParkourAction.WallrunRight : ParkourAction.WallrunLeft,
                    wallRunWallPoint = hit.point,
                    wallRunNormal = hit.normal,
                    wallSide = resolvedSide,
                    wallRunCollider = hit.collider,
                    playerRadius = Radius,
                    playerHeight = Height
                };
                return true;
            }

            #endregion

            #region Helpers

            Vector3 GetHeadingPlanar()
            {
                // Usa velocidad horizontal si existe; si no, forward de cámara proyectado al plano XZ
                if (rb)
                {
                    Vector3 hv = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                    if (hv.sqrMagnitude > 0.04f) return hv.normalized;
                }

                return Vector3.ProjectOnPlane(cameraHolder.forward, Vector3.up).normalized; // doc: ProjectOnPlane
            }

            Vector3 GetPlanarForward()
            {
                Vector3 f = cameraHolder ? cameraHolder.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
            }

            bool TopFromHit(in RaycastHit frontHit, out float topY)
            {
                float up = Mathf.Max(vaultMaxHeight + 0.5f, 1.6f);
                Vector3 upStart = frontHit.point + Vector3.up * up;
                if (Physics.Raycast(upStart, Vector3.down, out RaycastHit down,
                        up + 0.5f, environmentMask, QueryTriggerInteraction.Ignore))
                {
                    topY = down.point.y;
                    return true;
                }

                topY = 0f;
                return false;
            }

            bool HasClearanceCapsule(Vector3 center, float heightSegment)
            {
                float r = Radius - clearanceSkin * 0.5f; 
                float hSeg = Mathf.Max(0.01f, heightSegment); 
                float half = hSeg * 0.5f;

                Vector3 top = center + Vector3.up * half;
                Vector3 bottom = center - Vector3.up * half;

                return !Physics.CheckCapsule(top, bottom, r, environmentMask, QueryTriggerInteraction.Ignore);
            }

            private bool HasMinimumWallWidth(RaycastHit hit)
            {
                // Si el ancho mínimo es 0, saltamos el chequeo
                if (wallMinWidth <= 0f) return true;

                Vector3 wallNormal = hit.normal;
                Vector3 wallForward = Vector3.Cross(wallNormal, Vector3.up).normalized;

                // --- CORRECCIÓN: El origen debe estar FUERA de la pared ---
                // Usamos + wallNormal (hacia afuera) en lugar de - wallNormal (hacia adentro)
                Vector3 checkOriginBase = hit.point + wallNormal * wallWidthCheckDist;

                // Punto a la derecha (relativo al avance)
                Vector3 rightOrigin = checkOriginBase + wallForward * (wallMinWidth * 0.5f);

                // Punto a la izquierda (relativo al avance)
                Vector3 leftOrigin = checkOriginBase - wallForward * (wallMinWidth * 0.5f);

                // Distancia de casteo (un poco más que la distancia de origen)
                float castDist = wallWidthCheckDist + 0.05f;

                // Casteamos de vuelta hacia la pared (dirección -wallNormal)
                bool gotRight = Physics.Raycast(rightOrigin, -wallNormal, out var rightHit, castDist, environmentMask,
                    QueryTriggerInteraction.Ignore);
                bool gotLeft = Physics.Raycast(leftOrigin, -wallNormal, out var leftHit, castDist, environmentMask,
                    QueryTriggerInteraction.Ignore);

                // ----- DEBUG VISUAL -----
                if (drawGizmos)
                {
                    Color rightColor = gotRight ? Color.green : Color.red;
                    Color leftColor = gotLeft ? Color.green : Color.red;

                    // Dibuja los orígenes (una pequeña línea vertical)
                    Debug.DrawLine(rightOrigin, rightOrigin + Vector3.up * 0.1f, rightColor, 0.1f);
                    Debug.DrawLine(leftOrigin, leftOrigin + Vector3.up * 0.1f, leftColor, 0.1f);

                    // Dibuja los rayos
                    Debug.DrawRay(rightOrigin, -wallNormal * castDist, rightColor, 0.1f);
                    Debug.DrawRay(leftOrigin, -wallNormal * castDist, leftColor, 0.1f);
                }
                // ----- FIN DEBUG -----

                // Si ambos golpean Y sus normales son similares a la pared original, la superficie es ancha.
                if (gotRight && gotLeft &&
                    Vector3.Dot(rightHit.normal, wallNormal) > 0.9f &&
                    Vector3.Dot(leftHit.normal, wallNormal) > 0.9f)
                {
                    return true;
                }

                // Si uno de los dos falla, es un borde fino
                return false;
            }

            void OnDrawGizmosSelected()
            {
                if (!drawGizmos) return;
                
                if (capsule)
                {
                    GetCapsuleWorld(out var bottom, out var r);
                    Vector3 castOrigin = bottom + Vector3.up * r;
        
                    float castDistance = groundCheckDistance; 

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(castOrigin, r);

                    Vector3 endPosition = castOrigin + Vector3.down * castDistance;
                    Gizmos.DrawWireSphere(endPosition, r);

                    Gizmos.DrawLine(castOrigin, endPosition);

                    if (Application.isPlaying)
                    {
                        if (Grounded)
                        {
                            Gizmos.color = Color.green;
                            Gizmos.DrawWireSphere(GroundHit.point, r);
                            Gizmos.DrawRay(GroundHit.point, GroundHit.normal * 0.5f);
                        }
                        else
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawWireSphere(endPosition, r);
                        }
                    }
                }
                
                Gizmos.matrix = Matrix4x4.identity;

                Vector3 f = Application.isPlaying
                    ? GetPlanarForward()
                    : (transform.forward - Vector3.Project(transform.forward, Vector3.up)).normalized;
                Vector3 origin = transform.position +
                                 Vector3.up * Mathf.Clamp((capsule ? capsule.height : 1.8f) * 0.55f, 0.8f, 1.1f);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(origin, origin + f * forwardCheckDistance);

                var p = Probe;
                if (p.action != ParkourAction.None)
                {
                    Color c = p.action switch
                    {
                        ParkourAction.Vault => Color.green,
                        ParkourAction.Climb => new Color(0.2f, 0.9f, 1f),
                        ParkourAction.WallrunLeft => new Color(1f, 0.5f, 0.2f),
                        ParkourAction.WallrunRight => new Color(1f, 0.5f, 0.2f),
                        _ => Color.white
                    };
                    Gizmos.color = c;

                    if (p.action == ParkourAction.Vault)
                    {
                        Gizmos.DrawSphere(p.vaultTopPoint, 0.05f);
                        Gizmos.DrawSphere(p.vaultLandPoint, 0.05f);
                        Gizmos.DrawLine(p.vaultTopPoint, p.vaultLandPoint);

                        if (p.vaultLandOnSameCollider)
                        {
                            Gizmos.color = Color.magenta;
                            Gizmos.DrawWireSphere(p.vaultLandPoint, 0.06f);
                        }
                    }
                    else if (p.action == ParkourAction.Climb)
                    {
                        Gizmos.DrawSphere(p.climbLedgePoint, 0.06f);
                        Gizmos.DrawSphere(p.climbStandPoint, 0.06f);
                        Gizmos.DrawLine(p.climbLedgePoint, p.climbStandPoint);
                    }
                    else
                    {
                        Gizmos.DrawSphere(p.wallRunWallPoint, 0.06f);
                        Gizmos.DrawRay(p.wallRunWallPoint, p.wallRunNormal * 0.4f);
                    }
                }

                float mid = Mathf.Clamp((capsule ? capsule.height : 1.8f) * 0.5f, 0.8f, 1.0f);
                Vector3 sideOrigin = transform.position + Vector3.up * mid;
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(sideOrigin, sideOrigin + transform.right * wallCheckDistance);
                Gizmos.DrawLine(sideOrigin, sideOrigin - transform.right * wallCheckDistance);
            }

            #endregion
        }
    }
}