using System.Collections;
using UnityEngine;

namespace Player.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class CameraShake : MonoBehaviour
    {
        private Vector3 originalPos;
        private Coroutine shakeRoutine;

        private void Awake()
        {
            originalPos = transform.localPosition;
        }
        
        public void Shake(float duration, float magnitude)
        {
            if (shakeRoutine != null)
                StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeCoroutine(duration, magnitude));
        }
        public void PermanentShake()
        {
            Shake(20, 0.05f);
        }
        private IEnumerator ShakeCoroutine(float duration, float magnitude)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float damper = 1f - Mathf.Clamp01(progress);
                
                float offsetX = (Random.value * 2f - 1f) * magnitude * damper;
                float offsetY = (Random.value * 2f - 1f) * magnitude * damper;

                transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            
            transform.localPosition = originalPos;
            shakeRoutine = null;
        }
        public void StopShake()
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
                transform.localPosition = originalPos; // Asegurarte de que vuelve a la posición original
            }
        }
    }
}
