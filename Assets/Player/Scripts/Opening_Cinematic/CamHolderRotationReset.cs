using UnityEngine;

namespace Player.Scripts.Opening_Cinematic
{
    public class CamHolderRotationReset : MonoBehaviour
    {
        [Header("Rotación inicial (en grados)")]
        public Vector3 initialRotation = new Vector3(15f, 0f, 0f);

        [Header("Tiempo antes de iniciar el Lerp (segundos)")]
        public float waitTime = 2f;

        [Header("Duración del Lerp (segundos)")]
        public float lerpDuration = 1.5f;

        [Header("Curva de transición (0 a 1)")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Referencia al TutorialCameraSwitcher")]
        public TutorialCameraSwitcher tutorialCameraSwitcher;

        private Quaternion startRotation;
        private Quaternion targetRotation = Quaternion.identity;
        private bool isLerping;
        private float lerpTimer;
        private bool hasTriggeredLerp;

        void Start()
        {
            // Aplicar rotación inicial
            transform.localRotation = Quaternion.Euler(initialRotation);
            startRotation = transform.localRotation;
        }

        void Update()
        {
            if (tutorialCameraSwitcher == null)
                return;

            // Esperar a que el Animator del TutorialCameraSwitcher se active
            if (tutorialCameraSwitcher.animatorActivated && !hasTriggeredLerp)
            {
                hasTriggeredLerp = true;
                Invoke(nameof(BeginLerp), waitTime);
            }

            if (!isLerping)
                return;

            lerpTimer += Time.deltaTime;
            float t = Mathf.Clamp01(lerpTimer / lerpDuration);
            float curvedT = transitionCurve.Evaluate(t);

            transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, curvedT);

            if (t >= 1f)
                isLerping = false;
        }

        void BeginLerp()
        {
            // Reiniciar rotación y comenzar Lerp
            transform.localRotation = Quaternion.Euler(initialRotation);
            startRotation = transform.localRotation;
            lerpTimer = 0f;
            isLerping = true;
        }
    }
}





