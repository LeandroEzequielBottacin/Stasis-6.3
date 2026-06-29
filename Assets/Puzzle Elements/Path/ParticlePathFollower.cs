using System.Collections.Generic;
using Puzzle_Elements.Path.CurvedPathGenerator.Scripts;
using UnityEngine;

namespace Puzzle_Elements.Path
{
    public class ParticlePathFollower : MonoBehaviour
    {
        public PathGenerator pathGenerator;  // Referencia a tu PathGenerator
        public float speed = 5f;

        private List<Vector3> pathPoints;
        private int currentPointIndex;

        private void Start()
        {
            if (pathGenerator == null)
            {
                Debug.LogError("No hay PathGenerator asignado.");
                enabled = false;
                return;
            }

            pathPoints = pathGenerator.PathList;

            if (pathPoints == null || pathPoints.Count == 0)
            {
                Debug.LogError("La lista PathList est� vac�a.");
                enabled = false;
                return;
            }

            transform.position = pathPoints[0];  // Empieza en el primer punto
        }

        private void Update()
        {
            if (currentPointIndex >= pathPoints.Count)
            {
                currentPointIndex = 0;
                transform.position = pathPoints[0];
            }

            Vector3 target = pathPoints[currentPointIndex];
            float step = speed * Time.deltaTime;

            transform.position = Vector3.MoveTowards(transform.position, target, step);

            if (Vector3.Distance(transform.position, target) < 0.01f)
            {
                currentPointIndex++;  // Avanza al siguiente punto
            }
        }
    }
}