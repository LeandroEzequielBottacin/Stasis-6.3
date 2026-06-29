using UnityEngine;

namespace Player.Scripts.Opening_Cinematic
{
    public class CamHolderRotationResetAwakeWithDisable : MonoBehaviour
    {
        [Header("Rotación inicial (en grados)")]
        public Vector3 initialRotation = new Vector3(15f, 0f, 0f);

        [Header("Delay antes de iniciar el Lerp (segundos)")]
        public float startDelay = 2f;

        [Header("Duración del Lerp (segundos)")]
        public float lerpDuration = 1.5f;

        [Header("Curva de transición (0 a 1)")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Scripts a desactivar durante el Lerp")]
        public MonoBehaviour[] scriptsToDisable;

        private Quaternion startRotation;
        private Quaternion targetRotation = Quaternion.identity;
        private bool isLerping;
        private float lerpTimer;

        void Awake()
        {
            // Aplicar rotación inicial
            transform.localRotation = Quaternion.Euler(initialRotation);
            startRotation = transform.localRotation;

            // Desactivar scripts indicados
            if (scriptsToDisable != null)
            {
                foreach (var script in scriptsToDisable)
                {
                    if (script != null)
                        script.enabled = false;
                }
            }

            // Iniciar el lerp después del delay
            Invoke(nameof(BeginLerp), startDelay);
        }

        void Update()
        {
            if (!isLerping)
                return;

            lerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(lerpTimer / lerpDuration);

            float curvedT = transitionCurve.Evaluate(t);
            transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, curvedT);

            if (t >= 1f)
            {
                isLerping = false;
                ReactivateScripts();
            }
        }

        void BeginLerp()
        {
            // Volver a la rotación inicial antes de iniciar
            transform.localRotation = Quaternion.Euler(initialRotation);
            startRotation = transform.localRotation;

            isLerping = true;
            lerpTimer = 0f;
        }

        private void ReactivateScripts()
        {
            if (scriptsToDisable != null)
            {
                foreach (var script in scriptsToDisable)
                {
                    if (script != null)
                        script.enabled = true;
                }
            }
        }
    }
}

