using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.IK.Scripts
{
    [DisallowMultipleComponent]
    public class DirectionImpulseTrigger : MonoBehaviour
    {
        [Header("Direccion (from -> to)")]
        public Transform from;
        public Transform to;

        [Header("FollowTargetController")]
        [Tooltip("Si no se asigna, se busca en el padre.")]
        public FollowTargetController followController;

        [Header("Jugador a impulsar")]
        public Rigidbody playerRb;
        [Tooltip("Fallback por tag si playerRb es null.")]
        public string playerTag = "Player";
        public LayerMask playerMask = ~0; // opcional: capa del player

        [Header("Parametros del impulso")]
        public float forceMagnitude = 10f;
        public ForceMode forceMode = ForceMode.Impulse;
        public bool onlyHorizontalDirection = true;

        [Header("Retrigger")]
        [Tooltip("Si es false, dispara una unica vez (no vuelve a disparar en futuros false->true).")]
        public bool retriggerOnStop = true;

        [Header("Activacion por distancia")]
        [Tooltip("Activa el OverlapBox solo si el player esta a esta distancia o menos.")]
        public float activationDistance = 5f;

        [Header("OverlapBox config")]
        [Tooltip("Si hay BoxCollider y esto es true, usa su centro y tama�o locales.")]
        public bool useBoxCollider = true;
        [Tooltip("Si no usas BoxCollider, define el tama�o manual (en unidades).")]
        public Vector3 boxSize = new Vector3(1f, 0.5f, 1f);
        [Tooltip("Offset del centro del OverlapBox, en espacio local.")]
        public Vector3 boxCenterOffset = Vector3.up * 0.25f;

        [Header("Deteccion 'arriba' (sin contactos)")]
        [Tooltip("Requerir que el jugador este 'encima' (geometricamente) para disparar.")]
        public bool requirePlayerOnTop = true;
        [Tooltip("Umbral del dot((player - centro).normalized, up). 0.5 ~ 60�, 0.7 ~ 45�.")]
        [Range(0f, 1f)] public float minUpDot = 0.5f;
        [Tooltip("Si true usa Vector3.up; si false usa transform.up.")]
        public bool useWorldUp = true;

        [Header("Eventos (opcional)")]
        public UnityEvent onImpulseFired;

        [Header("Gizmos")]
        public bool showGizmos = true;
        public Color gizmoInactive = new Color(0f, 0.6f, 1f, 0.25f);
        public Color gizmoActive = new Color(0f, 1f, 0.2f, 0.35f);
        public Color gizmoWire = new Color(0f, 0f, 0f, 1f);

        // Estado
        [HideInInspector] public bool playerInContact;
        private bool prevCanMove;
        private bool overlapActive;

        // Cache
        private BoxCollider boxCol;

        private void Awake()
        {
            if (!followController)
                followController = GetComponentInParent<FollowTargetController>();

            if (!playerRb)
            {
                var go = GameObject.FindGameObjectWithTag(playerTag);
                if (go) playerRb = go.GetComponent<Rigidbody>();
            }

            boxCol = GetComponent<BoxCollider>();
        }

        private void FixedUpdate()
        {
            if (followController == null || from == null || to == null || playerRb == null)
                return;

            // 1) Activacion por distancia
            float distToPlayer = Vector3.Distance(playerRb.worldCenterOfMass, transform.position);
            overlapActive = distToPlayer <= activationDistance;

            // 2) Evaluar OverlapBox solo si activo
            if (overlapActive)
            {
                playerInContact = CheckPlayerOverlapBox(out bool isOnTop);
                bool canMoveNow = followController.canMove;

                // 3) Flanco ascendente: false -> true
                if (!prevCanMove && canMoveNow)
                {
                    bool allowed = playerInContact && (!requirePlayerOnTop || isOnTop);
                    if (allowed)
                    {
                        FireImpulse();

                        if (!retriggerOnStop)
                        {
                            prevCanMove = true; // latcheado
                            return;
                        }
                    }
                }
                prevCanMove = canMoveNow;
            }
            else
            {
                playerInContact = false;
                prevCanMove = followController.canMove; // mantener estado consistente
            }
        }

        private void FireImpulse()
        {
            Vector3 dir = to.position - from.position;
            if (onlyHorizontalDirection) dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;

            dir.Normalize();
            playerRb.AddForce(dir * forceMagnitude, forceMode);
            onImpulseFired?.Invoke();
        }

        private bool CheckPlayerOverlapBox(out bool isOnTop)
        {
            // Calcular centro y halfExtents en MUNDO, respetando rotacion del objeto
            Vector3 worldCenter;
            Vector3 halfExtents;
            Quaternion rot = transform.rotation;

            if (useBoxCollider && boxCol != null)
            {
                // Centro en mundo respetando offset local del BoxCollider
                worldCenter = transform.TransformPoint(boxCol.center + boxCenterOffset);
                // half extents aplicando escala
                Vector3 scaledSize = Vector3.Scale(boxCol.size, transform.lossyScale);
                halfExtents = scaledSize * 0.5f;
            }
            else
            {
                worldCenter = transform.TransformPoint(boxCenterOffset);
                Vector3 scaledSize = Vector3.Scale(boxSize, transform.lossyScale);
                halfExtents = scaledSize * 0.5f;
            }

            // Overlap con buffer minimo
            Collider[] hits = new Collider[8];
            int count = Physics.OverlapBoxNonAlloc(worldCenter, halfExtents, hits, rot, playerMask, QueryTriggerInteraction.Ignore);

            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (!rb) continue;

                // �Es el player?
                if (playerRb != null && rb == playerRb)
                {
                    found = true;
                    break;
                }
                if (playerRb == null && hits[i].CompareTag(playerTag))
                {
                    // si no teniamos cacheado el RB, guardalo
                    playerRb = rb;
                    found = true;
                    break;
                }
            }

            // Criterio de "arriba" geom�trico (sin normales de contacto)
            if (requirePlayerOnTop && playerRb != null)
            {
                Vector3 up = useWorldUp ? Vector3.up : transform.up;
                Vector3 toPlayer = (playerRb.worldCenterOfMass - worldCenter);
                float len = toPlayer.magnitude;
                float dot = (len > 1e-5f) ? Vector3.Dot(toPlayer / len, up) : 0f;
                isOnTop = dot >= minUpDot;
            }
            else
            {
                isOnTop = true;
            }

            return found;
        }

        // ---------------- Gizmos ----------------
        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            // Recalcular igual que en runtime (maneja falta de boxCol en edit)
            BoxCollider bc = useBoxCollider ? GetComponent<BoxCollider>() : null;
            Quaternion rot = transform.rotation;

            Vector3 worldCenter;
            Vector3 halfExtents;

            if (useBoxCollider && bc != null)
            {
                worldCenter = transform.TransformPoint(bc.center + boxCenterOffset);
                Vector3 scaledSize = Vector3.Scale(bc.size, transform.lossyScale);
                halfExtents = scaledSize * 0.5f;
            }
            else
            {
                worldCenter = transform.TransformPoint(boxCenterOffset);
                Vector3 scaledSize = Vector3.Scale(boxSize, transform.lossyScale);
                halfExtents = scaledSize * 0.5f;
            }

            // Cambiar color si est� activo (requiere playerRb en escena para evaluar distancia)
            bool active = overlapActive;
            if (!Application.isPlaying && playerRb != null)
            {
                float d = Vector3.Distance(playerRb.worldCenterOfMass, transform.position);
                active = d <= activationDistance;
            }

            Color fill = active ? gizmoActive : gizmoInactive;
            Gizmos.color = fill;
            Gizmos.matrix = Matrix4x4.TRS(worldCenter, rot, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);

            Gizmos.color = gizmoWire;
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);

            // Radio de activaci�n (opcional)
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(transform.position, activationDistance);
        }
    }
}
