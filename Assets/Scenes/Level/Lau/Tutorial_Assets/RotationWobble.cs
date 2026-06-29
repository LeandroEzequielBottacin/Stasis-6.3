using UnityEngine;

namespace Scenes.Level.Lau.Tutorial_Assets
{
    public class RotationWobble : MonoBehaviour
    {
        [Header("Eje de oscilación (solo 1 eje)")]
        public Axis swingAxis = Axis.X;

        [Header("Fuerza de meceo (grados)")]
        public float amplitude = 15f;

        [Header("Velocidad del meceo")]
        public float speed = 2f;

        [Header("Delay aleatorio de inicio")]
        public float minDelay;
        public float maxDelay = 1f;

        private Quaternion _initialRotation;
        private float _randomOffset;

        public enum Axis { X, Y, Z }

        void Start()
        {
            _initialRotation = transform.localRotation;

            // Offset aleatorio para desfasar el seno
            _randomOffset = Random.Range(minDelay, maxDelay);
        }

        void Update()
        {
            // Tiempo con offset aleatorio
            float t = Time.time * speed + _randomOffset;

            float angle = Mathf.Sin(t) * amplitude;

            Vector3 rot = Vector3.zero;

            switch (swingAxis)
            {
                case Axis.X: rot = new Vector3(angle, 0, 0); break;
                case Axis.Y: rot = new Vector3(0, angle, 0); break;
                case Axis.Z: rot = new Vector3(0, 0, angle); break;
            }

            transform.localRotation = _initialRotation * Quaternion.Euler(rot);
        }
    }
}


