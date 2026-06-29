using Player.Scripts.MovementFSM.MVC;
using UnityEngine;

namespace Puzzle_Elements.Sistema_tren.Scripts.Tren
{
    [RequireComponent(typeof(Collider))]
    public class ElevatorStickyZone : MonoBehaviour
    {
        [Tooltip("Rigidbody de la plataforma (si no se asigna, se toma autom�ticamente).")]
        public Rigidbody platformRb;
        public Collider _smallCollider;
        public Collider _bigCollider;
        [Tooltip("Solo para depuraci�n: muestra el modelo del jugador detectado.")]
        public Model debugModel;

        [Header("Box Detect (OverlapBox sobre la plataforma)")]
        [Tooltip("Altura del volumen de detecci�n sobre la plataforma.")]
        public float castDistance = 0.6f;

        [Tooltip("Distancia desde el top de la plataforma hasta donde empieza el volumen de detecci�n.")]
        public float castStartOffset = 0.02f;

        [Tooltip("Margen adicional en los ejes X y Z para el volumen de detecci�n.")]
        public Vector2 castPaddingXZ = new Vector2(0.05f, 0.05f);

        [Tooltip("M�scara de capas que detectar� el OverlapBox.")]
        public LayerMask castMask = ~0;

        [Tooltip("Si est� activado, dibuja el volumen de detecci�n en la escena.")]
        public bool drawGizmos = true;

        [Header("Hover")]
        [Tooltip("Altura fija desde el top de la plataforma hasta los pies del jugador.")]
        public float hoverOffset = 0.2f;

        [Header("Suavizado opcional de la velocidad de la plataforma (para c�lculos de v.y)")]
        [Tooltip("Suavizado (en segundos) de la velocidad vertical de la plataforma. 0 = sin suavizado.")]
        public float platVelSmoothSeconds;

        Rigidbody _playerRb;
        Collider _playerCol;
        Collider _col;

        float _lastPlatY;
        float _smoothedPlatVy;
        [Header("Volumen de detecci�n manual (no depende del collider)")]
        [Tooltip("Centro del volumen local respecto a la plataforma.")]
        public Vector3 customBoxCenter = new Vector3(0f, 1f, 0f);

        [Tooltip("Tama�o total del volumen de detecci�n (en unidades).")]
        public Vector3 customBoxSize = new Vector3(1f, 0.6f, 1f);
        void Awake()
        {
            if (platformRb == null) platformRb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
            if (_col && _col.isTrigger) _col.isTrigger = false;

            _lastPlatY = platformRb ? platformRb.position.y : 0f;
            _smoothedPlatVy = 0f;
        }

        void FixedUpdate()
        {
            if (platformRb == null || _col == null) return;

            // Velocidad vertical (suavizada) de la plataforma: la usamos solo para estabilizar v.y del player
            float platY = platformRb.position.y;
            float rawVy = (platY - _lastPlatY) / Time.fixedDeltaTime;
            float alpha = (platVelSmoothSeconds <= 0f)
                ? 1f
                : Mathf.Clamp01(Time.fixedDeltaTime / (platVelSmoothSeconds + Time.fixedDeltaTime));
            _smoothedPlatVy = Mathf.Lerp(_smoothedPlatVy, rawVy, alpha);

            // Detecci�n
            bool detected = TryDetectPlayer(out Rigidbody hitRb, out Model hitModel, out Collider hitCol);

            if (detected)
            {
                if (_playerRb != hitRb)
                {
                    _playerRb = hitRb;
                    _playerCol = hitCol != null ? hitCol : hitRb.GetComponentInChildren<Collider>();
                }
                debugModel = hitModel;

                if (_playerRb != null && _playerCol != null)
                {
                    // 1) Apagar gravedad mientras est� �anclado� al hover para que no pelee contra el snap
               
               
                    // 2) Altura objetivo: pies del jugador a hoverOffset sobre el top de la plataforma
                    float topY = _col.bounds.max.y;
                    float feetY = _playerCol.bounds.min.y;
                    float wantY = topY + hoverOffset;
                    float deltaY = wantY - feetY; // cu�nto hay que mover en Y para dejar los pies en wantY

                    // 3) Mover SOLO en Y (X/Z libres para que el jugador se mueva lateralmente)
                    Vector3 p = _playerRb.position;
                    if (platformRb.linearVelocity.magnitude > 0.002f)
                    {
                        _playerRb.useGravity = false;
                        _bigCollider.enabled = false;
                        _smallCollider.enabled = true;
                        _playerRb.MovePosition(new Vector3(p.x, p.y , p.z));
                   


                    }
                    else
                    {
                        _bigCollider.enabled = true;
                        _smallCollider.enabled = false;
                        _playerRb.useGravity = true;
                    }
               

                    // 4) Ajustar la velocidad vertical para acompa�ar el movimiento general del elevador
                    //    (no empujar hacia arriba si la plataforma cae)
                    var v = _playerRb.linearVelocity;
                    float targetVy = _smoothedPlatVy;
                    // si la plataforma sube, acompa�amos; si baja, nunca ponemos v.y positiva
                    if (targetVy > 0f)
                        v.y = targetVy;
                    else
                        v.y = Mathf.Min(v.y, targetVy);

                    if(platformRb.linearVelocity.magnitude >0.02f)
                        _playerRb.linearVelocity = v;
                }
            }
            else
            {
                // No detectado: devolver control
                if (_playerRb != null) _playerRb.useGravity = true;
                _playerRb = null;
                _playerCol = null;
                debugModel = null;
            }

            _lastPlatY = platY;
        }

        // OverlapBox para detectar jugador
        bool TryDetectPlayer(out Rigidbody rb, out Model m, out Collider playerCol)
        {
            rb = null; m = null; playerCol = null;

            // --- AHORA: No depende del collider ---
            Vector3 half = new Vector3(
                customBoxSize.x * 0.5f,
                customBoxSize.y * 0.5f,
                customBoxSize.z * 0.5f
            );

            Vector3 center = transform.TransformPoint(customBoxCenter);

            var hits = Physics.OverlapBox(center, half, transform.rotation, castMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i];
                var hrb = c.attachedRigidbody;
                if (hrb == null) continue;

                var hm = hrb.GetComponent<Model>();
                if (hm == null) continue;

                rb = hrb;
                m = hm;
                playerCol = hrb.GetComponent<Collider>() ?? hrb.GetComponentInChildren<Collider>() ?? c;
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Vector3 half = customBoxSize * 0.5f;
            Vector3 center = transform.TransformPoint(customBoxCenter);

            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, customBoxSize);
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
