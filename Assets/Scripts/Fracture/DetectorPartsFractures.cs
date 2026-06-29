using System.Collections.Generic;
using Fracture.Destruction_System.Refractored;
using UnityEngine;

namespace Fracture
{
    public class DetectorPartsFractures : MonoBehaviour
    {
        [SerializeField] private StasisGunEntity diosa1;
        [SerializeField] private StasisGunEntity diosa2;

        [Header("Parámetros de detección")]
        [SerializeField] private Vector3 boxHalfExtents = new Vector3(1f, 1f, 1f);
        [SerializeField] private Vector3 boxCenterOffset = new Vector3(0f, 0f, 2f);
        [SerializeField] private LayerMask detectionLayer;

        [SerializeField] private Collider[] hits = new Collider[40];
        private HashSet<DestroyedPieceController> alreadyDetected = new HashSet<DestroyedPieceController>();
        private HashSet<int> alreadyDetectedID = new HashSet<int>();

        void Update()
        {
            Vector3 boxCenter = transform.position + transform.TransformDirection(boxCenterOffset);
            int count = Physics.OverlapBoxNonAlloc(boxCenter, boxHalfExtents, hits, transform.rotation, detectionLayer);

            for (int i = 0; i < count; i++)
            {
                DestroyedPieceController part = hits[i].GetComponent<DestroyedPieceController>();
                if (part != null && !alreadyDetectedID.Contains(part.ID))
                {
                    if (part.is_connected) return;

                    ChooseGod(part);

                    alreadyDetectedID.Add(part.ID);
                    part.alreadyColision = true;
                }
            }
        }

        public void ChooseGod(DestroyedPieceController part)
        {
            int randomValue = Random.Range(0, 2);

            if (randomValue == 0)
                diosa1.TryStasis(part.transform, part);
            else
                diosa2.TryStasis(part.transform, part);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

            Vector3 boxCenter = transform.position + transform.TransformDirection(boxCenterOffset);
            Gizmos.matrix = Matrix4x4.TRS(boxCenter, transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, boxHalfExtents * 2);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, boxHalfExtents * 2);
        }
    }
}
