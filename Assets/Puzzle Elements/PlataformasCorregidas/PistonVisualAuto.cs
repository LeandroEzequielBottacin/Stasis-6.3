// using UnityEngine;
//
// [DisallowMultipleComponent]
// public class PistonVisualAuto : MonoBehaviour
// {
//     public enum Axis { X, Y, Z }
//
//     [Header("Anclajes")]
//     public Transform baseAnchor;
//     public Transform platformTopAnchor;
//
//     [Header("Piezas visuales")]
//     public Transform rodTube;
//     public Transform headCap;
//
//     [Header("Alineaci�n y escala")]
//     public Axis stretchAxis = Axis.Y;
//     public float minRodLength = 0.1f;
//     public float lengthOffset = 0.0f;
//     public bool keepVertical = true;
//
//     Vector3 _baseScale = Vector3.one;
//
//     [SerializeField] private ElevatorShipmentTrain _elevatorShipmentTrain;
//     void Awake()
//     {
//         if (rodTube) _baseScale = rodTube.localScale;
//         _elevatorShipmentTrain = GetComponentInParent<ElevatorShipmentTrain>();
//     }
//     void FixedUpdate()
//     {
//         if (!baseAnchor || !platformTopAnchor || !rodTube || _elevatorShipmentTrain.IsFreezed || !_elevatorShipmentTrain.canMove) return;
//
//         Vector3 a = baseAnchor.position;
//         Vector3 b = platformTopAnchor.position;
//         Vector3 dir = b - a;
//         float dist = dir.magnitude;
//         if (dist < 1e-5f) dist = 0f;
//
//         if (!keepVertical && dist > 1e-5f) rodTube.up = dir.normalized;
//         else rodTube.up = Vector3.up;
//
//         rodTube.position = a + dir * 0.5f;
//
//         Vector3 s = _baseScale;
//         float L = Mathf.Max(minRodLength, dist + lengthOffset);
//         switch (stretchAxis)
//         {
//             case Axis.X: s.x = L; break;
//             case Axis.Y: s.y = L; break;
//             case Axis.Z: s.z = L; break;
//         }
//         rodTube.localScale = s;
//
//         if (headCap)
//         {
//             headCap.position = b;
//             headCap.up = (!keepVertical && dist > 1e-5f) ? dir.normalized : Vector3.up;
//         }
//     }
// }

using Puzzle_Elements.Tren_nuevo;
using UnityEngine;

namespace Puzzle_Elements.PlataformasCorregidas
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class PistonVisualAuto : MonoBehaviour
    {
        public enum Axis { X, Y, Z }

        [Header("Anclajes")]
        public Transform baseAnchor;           // Punto fijo del pistón (base)
        public Transform platformTopAnchor;    // Punto móvil (tope de la plataforma)

        [Header("Piezas visuales")]
        public Transform rodTube;              // Tubo/barra que se estira
        public Transform headCap;              // Tapita/cabezal que va en el extremo móvil

        [Header("Alineación y escala")]
        public Axis stretchAxis = Axis.Y;      // Eje local del rodTube que se estira
        [Min(0f)] public float minRodLength = 0.1f;
        public float lengthOffset;      // Offset adicional de largo
        public bool keepVertical = true;       // Si true, no orienta hacia el destino; usa Vector3.up

        [Header("Actualización")]
        public bool useFixedUpdate = true;     // Si el movimiento viene de Rigidbody, conviene FixedUpdate

        [Tooltip("Compensa (aprox.) escalas no uniformes en padres. Déjalo OFF salvo que lo necesites.")]
        public bool compensateParentScale;

        [SerializeField] private ElevatorShipmentTrain _elevatorShipmentTrain;

        Vector3 _baseScale = Vector3.one;

        void Awake()
        {
            CacheBaseScale();
            // Puede no existir en modo edición; lo buscamos si no está asignado
            if (_elevatorShipmentTrain == null)
                _elevatorShipmentTrain = GetComponentInParent<ElevatorShipmentTrain>();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            CacheBaseScale();
        }
#endif

        void CacheBaseScale()
        {
            if (rodTube != null)
                _baseScale = rodTube.localScale;
        }

        void Update()
        {
            if (!useFixedUpdate)
                Tick();
        }

        void FixedUpdate()
        {
            if (useFixedUpdate)
                Tick();
        }

        void Tick()
        {
            if (rodTube == null || baseAnchor == null || platformTopAnchor == null)
                return;

            // Si existe el sistema de elevador, respetamos flags; si no, seguimos igual
            if (_elevatorShipmentTrain != null)
            {
                if (_elevatorShipmentTrain.IsFreezed || !_elevatorShipmentTrain.canMove)
                    return;
            }

            Vector3 a = baseAnchor.position;
            Vector3 b = platformTopAnchor.position;
            Vector3 dir = b - a;
            float dist = dir.magnitude;

            // Evita NaNs y divisiones raras
            if (dist < 1e-5f)
                dist = 0f;

            // Orientación del tubo
            if (!keepVertical && dist > 1e-5f)
                rodTube.up = dir.normalized;
            else
                rodTube.up = Vector3.up;

            // Posición al medio entre a y b (asume pivot centrado)
            rodTube.position = a + dir * 0.5f;

            // Calcular el largo deseado
            float L = Mathf.Max(minRodLength, dist + lengthOffset);

            // Escala local partiendo de la escala base
            Vector3 s = _baseScale;

            // (Opcional) compensación aproximada por escala de padres: útil si tenés non-uniform scale
            if (compensateParentScale)
            {
                // Nota: esto es una aproximación porque la rotación del rod puede mezclar ejes.
                // Úsalo solo si realmente lo necesitás.
                Vector3 lossy = rodTube.lossyScale;
                float axisLossy = AxisComponent(lossy, stretchAxis);
                if (axisLossy <= 1e-5f) axisLossy = 1f;
                L /= axisLossy;
            }

            // Asignar el largo al eje seleccionado
            switch (stretchAxis)
            {
                case Axis.X: s.x = L; break;
                case Axis.Y: s.y = L; break;
                case Axis.Z: s.z = L; break;
            }
            rodTube.localScale = s;

            // Cabezal en el extremo móvil
            if (headCap)
            {
                headCap.position = b;
                headCap.up = (!keepVertical && dist > 1e-5f) ? dir.normalized : Vector3.up;
            }
        }

        float AxisComponent(in Vector3 v, Axis axis, float fallback = 1f)
        {
            switch (axis)
            {
                case Axis.X: return Mathf.Abs(v.x);
                case Axis.Y: return Mathf.Abs(v.y);
                case Axis.Z: return Mathf.Abs(v.z);
                default: return fallback;
            }
        }

#if UNITY_EDITOR
        // Gizmos para depurar en escena
        void OnDrawGizmos()
        {
            if (baseAnchor && platformTopAnchor)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(baseAnchor.position, platformTopAnchor.position);
                Gizmos.DrawSphere(baseAnchor.position, 0.02f);
                Gizmos.DrawSphere(platformTopAnchor.position, 0.02f);
            }

            if (rodTube)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(rodTube.position, Vector3.one * 0.05f);
            }
        }
#endif
    }
}
