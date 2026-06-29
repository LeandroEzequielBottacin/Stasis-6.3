using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Puzzle_Elements.Spider
{
    [DefaultExecutionOrder(50)]
    public class SpiderFoot : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Punto 'ideal' donde esta pata quiere estar en reposo (anclado al cuerpo).")]
        [SerializeField] private Transform placementTarget;  
        [Tooltip("Transform del cuerpo para 'up' y c�lculos relativos (SpiderBodyParent).")]
        [SerializeField] private Transform bodyTransform;
        [Tooltip("Pata opuesta para evitar levantar dos a la vez (LLx RRx).")]
        [SerializeField] private SpiderFoot opposingFoot;

        [Header("Terreno")]
        [Tooltip("Capas del suelo donde la pata puede apoyarse.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Altura desde la que lanzamos el raycast hacia abajo (desde placementTarget).")]
        [SerializeField] private float groundRaycastHeight = 0.5f;
        [Tooltip("Distancia m�xima del raycast hacia abajo.")]
        [SerializeField] private float groundRaycastMaxDist = 2.0f;

        [Header("Paso / Gait")]
        [Tooltip("Distancia a partir de la cual la pata decide dar un paso.")]
        [SerializeField] private float stepThreshold = 0.6f;
        [Tooltip("Tiempo m�nimo entre pasos de esta pata.")]
        [SerializeField] private float stepCooldown = 0.14f;
        [Tooltip("No intentes pisar si el destino tiene un desnivel vertical mayor a esto.")]
        [SerializeField] private float maxVerticalDeltaForStep = 0.25f;

        [Header("Movimiento del pie")]
        [Tooltip("Duraci�n total del paso (segundos).")]
        [SerializeField, Min(0.01f)] private float stepDuration = 0.18f;
        [Tooltip("Altura m�xima del arco respecto a la l�nea recta del paso.")]
        [SerializeField] private float stepLiftHeight = 0.10f;
        [Tooltip("Curva de altura del arco (0..1 -> 0..1).")]
        [SerializeField] private AnimationCurve liftCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Tooltip("Suavizado de entrada/salida horizontal (0..1 -> 0..1).")]
        [SerializeField] private AnimationCurve horizCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Debug")]
        [SerializeField] private bool debugGizmos = true;
        [SerializeField] private Color restColor = new(0.2f, 0.9f, 0.2f, 0.9f);
        [SerializeField] private Color stepColor = new(0.9f, 0.7f, 0.1f, 0.9f);

        [Header("Eventos")]
   
        public UnityEvent<bool> OnPlantedChange;

        private TerrainDetector _detector;
 
        public bool IsStepping { get; private set; }
        public bool IsPlanted => !IsStepping;

   
        public bool StepGateEnabled { get; set; } = true;

        public Vector3 LastGroundPoint { get; private set; }
        public Vector3 LastGroundNormal { get; private set; }

        private Vector3 _lastPlantedPosition;
        private float _lastStepTime = -999f;
        private RaycastHit _lastGroundHit;

        void Start()
        {
            if (placementTarget && SampleGround(placementTarget.position, out RaycastHit hit))
                _lastPlantedPosition = hit.point;
            else
                _lastPlantedPosition = transform.position;

            transform.position = _lastPlantedPosition;
            OnPlantedChange?.Invoke(true);
        }

        void Update()
        {
            if (IsStepping) return; 
            if (!StepGateEnabled) return;

            if (opposingFoot != null && opposingFoot.IsStepping) return;

            if (Time.time - _lastStepTime < stepCooldown) return;

            Vector3 desired = GetDesiredFootPoint();
            float dist = Vector3.Distance(transform.position, desired);
            if (dist <= stepThreshold) return;

            if (!SampleGround(desired, out RaycastHit hit)) return;

            if (Mathf.Abs(hit.point.y - transform.position.y) > maxVerticalDeltaForStep) return;

            StartCoroutine(StepTo(hit.point));
        }

        private bool SampleGround(Vector3 from, out RaycastHit hit)
        {
            Vector3 up = bodyTransform ? bodyTransform.up : Vector3.up;

            Vector3 origin = from + up * groundRaycastHeight;
            Vector3 dir = -up;

            if (Physics.Raycast(origin, dir, out hit, groundRaycastMaxDist + groundRaycastHeight, groundMask, QueryTriggerInteraction.Ignore))
            {
                _lastGroundHit = hit;
                LastGroundPoint = hit.point;
                LastGroundNormal = hit.normal;
                return true;
            }

            origin = from + Vector3.up * groundRaycastHeight;
            if (Physics.Raycast(origin, Vector3.down, out hit, groundRaycastMaxDist + groundRaycastHeight, groundMask, QueryTriggerInteraction.Ignore))
            {
                _lastGroundHit = hit;
                LastGroundPoint = hit.point;
                LastGroundNormal = hit.normal;
                return true;
            }

            return false;
        }

        private Vector3 GetDesiredFootPoint()
        {
            if (!placementTarget) return transform.position;

            if (SampleGround(placementTarget.position, out RaycastHit hit))
                return hit.point;

            return placementTarget.position;
        }

        private IEnumerator StepTo(Vector3 targetGround)
        {
            IsStepping = true;
            OnPlantedChange?.Invoke(false); 

            _lastStepTime = Time.time;

            Vector3 start = transform.position;
            Vector3 end = targetGround;
            Vector3 up = bodyTransform ? bodyTransform.up : Vector3.up;

            float t = 0f;
            float invDur = 1f / Mathf.Max(0.01f, stepDuration);

            while (t < 1f)
            {
                t += Time.deltaTime * invDur;
                float ht = liftCurve.Evaluate(Mathf.Clamp01(t));   
                float kt = horizCurve.Evaluate(Mathf.Clamp01(t));  

                Vector3 flat = Vector3.LerpUnclamped(start, end, kt);
                Vector3 lifted = flat + up * (Mathf.Sin(Mathf.PI * ht) * stepLiftHeight);

                transform.position = lifted;
                yield return null;
            }

            transform.position = end;
            _lastPlantedPosition = end;

            IsStepping = false;
            OnPlantedChange?.Invoke(true); 
        }

        private void OnDrawGizmos()
        {
            if (!debugGizmos) return;

            if (placementTarget)
            {
                Vector3 up = bodyTransform ? bodyTransform.up : Vector3.up;
                Vector3 o = placementTarget.position + up * groundRaycastHeight;
                Vector3 d = -up * (groundRaycastMaxDist + groundRaycastHeight);

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(o, o + d);
                Gizmos.DrawWireSphere(o, 0.02f);

                Vector3 desired = Application.isPlaying ? GetDesiredFootPoint() : placementTarget.position;
                Gizmos.color = IsStepping ? stepColor : restColor;
                Gizmos.DrawSphere(desired, 0.025f);
            }

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, 0.02f);

            if (_lastGroundHit.collider != null)
            {
                Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.6f);
                Gizmos.DrawWireSphere(_lastGroundHit.point, 0.0175f);
            }
        }
    }
}
