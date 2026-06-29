using UnityEngine;

namespace Scenes.Level.Lau.Tutorial_Assets
{
    public class WagonSwingRotation : MonoBehaviour
    {
        [Header("Referencia al script de movimiento del vagón")]
        public WagonMover wagonMover;

        [Header("Swing mientras avanza")]
        public float swingAngle = 5f;       // ángulo máximo de swing lateral
        public float swingFrequency = 2f;   // velocidad del swing lateral

        [Header("Swing al final")]
        public float endSwingAngle = 8f;    // ángulo máximo del rebote final
        public float endSwingDuration = 0.5f; // duración del rebote final

        private float swingTimer;
        private float endSwingTimer;
        private Quaternion initialRotation;

        void Start()
        {
            if (wagonMover != null)
                initialRotation = wagonMover.transform.rotation;
        }

        void LateUpdate()
        {
            if (wagonMover == null)
                return;

            if (!wagonMover.hasReachedEnd)
            {
                // Swing lateral mientras avanza (sobre Z)
                swingTimer += Time.deltaTime * swingFrequency;
                float angleOffsetZ = Mathf.Sin(swingTimer) * swingAngle;
                transform.rotation = initialRotation * Quaternion.Euler(0f, 0f, angleOffsetZ);
            }
            else
            {
                // Swing de rebote al final (sobre X)
                if (endSwingTimer < endSwingDuration)
                {
                    endSwingTimer += Time.deltaTime;
                    float t = endSwingTimer / endSwingDuration;
                    float angleOffsetX = Mathf.Sin(t * Mathf.PI * 2f) * endSwingAngle * (1f - t);
                    transform.rotation = initialRotation * Quaternion.Euler(angleOffsetX, 0f, 0f);
                }
                else
                {
                    transform.rotation = initialRotation;
                }
            }
        }
    }
}




