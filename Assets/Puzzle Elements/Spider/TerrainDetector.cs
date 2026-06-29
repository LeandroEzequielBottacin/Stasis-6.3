using UnityEngine;

namespace Puzzle_Elements.Spider
{
    public class TerrainDetector : MonoBehaviour
    {
        private Vector3 _hitPos = Vector3.zero;

        [SerializeField] private float _castDistance = 2f;
        [SerializeField] private LayerMask _terrainLayer = ~0;
        int _numberOfHits = 0;
        [SerializeField] private float _castHeight = 3f;

        // --- API p�blica ---
        public Vector3 GetHitPoint()
        {
            RaycastHit hit;
            Ray ray = new Ray(transform.position + (transform.up * _castHeight), -transform.up);
            Physics.Raycast(ray, out hit, _castDistance + _castHeight, _terrainLayer);

            if (hit.collider != null) _hitPos = hit.point;
            else _hitPos = transform.position + (transform.up * _castHeight);

            return _hitPos;
        }

        // --- Gizmos ---
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + transform.up * _castHeight;
            Vector3 dir = -transform.up;
            float maxDist = _castDistance + _castHeight;

            // l�nea del rayo
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f); // celeste
            Gizmos.DrawLine(origin, origin + dir * maxDist);

            // esfera en el origen y en el extremo
            Gizmos.DrawWireSphere(origin, 0.06f);
            Gizmos.DrawWireSphere(origin + dir * maxDist, 0.06f);

            // si pega, dibujamos punto e indicamos normal
            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, _terrainLayer, QueryTriggerInteraction.Ignore))
            {
                // punto de impacto
                Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.95f); // verde
                Gizmos.DrawSphere(hit.point, 0.05f);

                // normal
                Vector3 n = hit.normal;
                float nLen = 0.35f;
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.95f); // amarillo
                Gizmos.DrawLine(hit.point, hit.point + n * nLen);
                Gizmos.DrawWireSphere(hit.point + n * nLen, 0.03f);
            }
            else
            {
                // sin impacto: marca el �fallback� (la posici�n que devolver�a GetHitPoint)
                Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f); // rojo
                Vector3 fallback = transform.position + (transform.up * _castHeight);
                Gizmos.DrawWireSphere(fallback, 0.05f);
            }
        }
    }
}
