using Managers.Game;
using Player.Scripts.MovementFSM.MVC;
using Puzzle_Elements.Tren_nuevo;
using UnityEngine;

namespace Puzzle_Elements.Sistema_tren.Scripts.Tren
{
    [RequireComponent(typeof(Collider))]
    public class ElevatorSticky_AnimatorDriven : MonoBehaviour
    {
        public Transform t;
        [Header("Platform Colliders (switch por movimiento Y)")]
        public Collider _smallCollider; // Activo mientras la plataforma SE MUEVE en Y
        public Collider _bigCollider;   // Activo mientras la plataforma est� QUIETA

        [Tooltip("Solo depuraci�n: muestra el Model detectado.")]
        public Model debugModel;

        [Header("Box Detect (OverlapBox)")]
        public LayerMask castMask = ~0;
        public bool drawGizmos = true;
        public Vector3 customBoxCenter = new Vector3(0f, 1f, 0f);
        public Vector3 customBoxSize = new Vector3(1f, 0.6f, 1f);

        [Header("Hover")]
        [Tooltip("Distancia m�nima de los pies del player a la cara superior de la plataforma.")]
        public float hoverOffset = 0.2f;

        [Header("Correcci�n vertical")]
        [Tooltip("Ganancia temporal para corregir hacia el hover (m�s alto = m�s r�pido).")]
        [Range(0f, 40f)] public float verticalGain = 12f;

        [Header("Detecci�n de movimiento (Y)")]
        [Tooltip("Velocidad m�nima (m/s) para considerar que la plataforma se mueve en Y.")]
        public float ySpeedThreshold = 0.01f;
        [Tooltip("Tiempo m�nimo movi�ndose para activar el estado Moving.")]
        public float minMoveEnterTime = 0.06f;
        [Tooltip("Tiempo m�nimo quieta para volver al estado Idle.")]
        public float minMoveExitTime = 0.12f;

        [Header("Robustez de detecci�n")]
        [Tooltip("Mantiene al player enganchado si el OverlapBox falla brevemente.")]
        public float keepWhileMovingCoyote = 0.08f;

        // Internos
        private Collider _platformCol;
        [SerializeField] private Rigidbody _playerRb;
        private Collider _playerCol;

        private float _lastY;
        private float _yVel; // m/s
        private float _stateTimer;
        private float _lastDetectTime;
        [SerializeField] private float verticalVelocity;
        [SerializeField] private float smoothAmount = 0.1f;
        private ElevatorShipmentTrain _elevatorShipmentTrain;
        // Buffer Overlap (evita GC)
        private readonly Collider[] _hits = new Collider[8];

        void Awake()
        {
            _platformCol = GetComponent<Collider>();
            if (_platformCol && _platformCol.isTrigger) _platformCol.isTrigger = false;

            _lastY = transform.position.y;
            //ApplyState(MoveState.Idle, force: true);
            _elevatorShipmentTrain = GetComponentInParent<ElevatorShipmentTrain>();
        }
   
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // 1) Medir velocidad vertical (m/s) del frame de f�sica
            float y = transform.position.y;
            _yVel = (y - _lastY) / Mathf.Max(dt, 0.001f);
            _lastY = y;

            // 2) FSM con hist�resis por tiempo (enter/exit)
            bool movingNow = Mathf.Abs(_yVel) >= ySpeedThreshold;
            _stateTimer += dt;



            bool detected = TryDetectPlayer(out Rigidbody hitRb, out Model hitModel, out Collider hitCol);
            if (detected && _elevatorShipmentTrain.canMove)
            {
                if (_playerRb != hitRb)
                {
                    _playerRb = hitRb;
                    _playerCol = hitCol != null ? hitCol : hitRb.GetComponentInChildren<Collider>();
                }
                debugModel = hitModel;
            }



            bool coyoteHold = (Time.time - _lastDetectTime) <= keepWhileMovingCoyote;

            if ((_playerRb != null && _playerCol != null) && _elevatorShipmentTrain.canMove)
            {
                Vector3 p = GameManager.Instance.player.position;
                _playerRb.useGravity = false;
                Debug.Log("MOVIENDO");

                float targetY = t.position.y + hoverOffset;
                Vector3 newPos = _playerRb.position;
                newPos.y = Mathf.SmoothDamp(newPos.y, targetY, ref verticalVelocity, smoothAmount);
                _playerRb.MovePosition(newPos);

            }
            else
            {
                // Perdimos al player por mucho: liberamos
                ReleasePlayerIfAny();
            }
        }

        //
        void ReleasePlayerIfAny()
        {
            if (_playerRb != null)
            {
                // Fallback conservador

                _playerRb.isKinematic = false;
                _playerRb.useGravity = true;
                _playerRb.linearVelocity = Vector3.zero;
                _playerRb = null;
                _playerCol = null;
                debugModel = null;
            }
        }

        bool TryDetectPlayer(out Rigidbody rb, out Model m, out Collider playerCol)
        {
            rb = null; m = null; playerCol = null;

            Vector3 half = customBoxSize * 0.5f;
            Vector3 center = transform.TransformPoint(customBoxCenter);

            int count = Physics.OverlapBoxNonAlloc(
                center, half, _hits, transform.rotation,
                castMask, QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < count; i++)
            {
                var c = _hits[i];
                if (!c) continue;

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
