using UnityEngine;

namespace Scenes.Level.Lau.Tutorial_Assets
{
    public class WagonMover : MonoBehaviour
    {
        [Header("Puntos de movimiento en World Space")]
        public Vector3 startPoint;
        public Vector3 endPoint;
        public float speed = 5f;

        [HideInInspector]
        public bool hasReachedEnd;

        void Start()
        {
            transform.position = startPoint;
        }

        void FixedUpdate()
        {
            if (hasReachedEnd)
                return;

            transform.position = Vector3.MoveTowards(transform.position, endPoint, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, endPoint) < 0.01f)
            {
                transform.position = endPoint;
                hasReachedEnd = true;
            }
        }
    }
}

