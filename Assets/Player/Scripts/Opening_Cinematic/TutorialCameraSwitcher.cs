using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Player.Scripts.Opening_Cinematic
{
    public class TutorialCameraSwitcher : MonoBehaviour
    {
        [Header("📸 CÁMARAS VIRTUALES")]
        public CinemachineVirtualCameraBase cameraA;
        public CinemachineVirtualCameraBase cameraB;
        public CinemachineVirtualCameraBase cameraC;

        [Header("🧱 OBJETOS QUE CONTIENEN LAS CÁMARAS")]
        public GameObject objectA;
        public GameObject objectB;
        public GameObject objectC;

        [Header("🎥 MAIN CAMERA A DESACTIVAR EN B→C")]
        public GameObject mainCameraToDisable;

        [Header("⏱️ CONFIGURACIÓN DE TRANSICIONES")]
        public float blendTime = 1f;
        public StartCamera startCamera = StartCamera.CameraA;

        [Header("🔁 TRANSICIÓN MANUAL (DEBUG)")]
        public bool transitionToA;
        public bool transitionToB;
        public bool transitionToC;

        [Header("🎭 REFERENCIAS PRINCIPALES")]
        public RagdollHanger ragdollHanger;
        public GameObject objectToDeactivate;

        [Header("🖼️ CANVAS CONFIGURACIÓN")]
        public CanvasGroup canvasGroup;
        public float startFadeDelay = 2f;
        public float startFadeDuration = 5f;
        public float fadeBlackDelay = 2f;
        public float fadeBlackDuration = 5f;
        public Canvas extraCanvas;
        public float extraCanvasActiveTime = 3f;

        [Header("🎬 ANIMATOR CONFIGURACIÓN")]
        public Animator targetAnimator;
        public float animatorStartDelay = 1f;
        public float transitionAfterAnimatorDelay = 3f;

        [Header("🧩 SCRIPTS A ACTIVAR DESPUÉS DE LA TRANSICIÓN B→C")]
        public MonoBehaviour[] scriptsToEnable;
        public float scriptsActivationDelay = 1f;

        private bool isTransitioning;
        private bool prevA, prevB, prevC;
        private bool fadeBlackPrev;

        // Canvas
        private bool hasStartedFade;
        private bool hasReappeared;
        private bool hasFadedAfterBlack;
        private bool hasActivatedExtraCanvas;

        // Animator
        private bool animatorStarted;
        public bool animatorActivated;

        public enum StartCamera
        {
            CameraA,
            CameraB,
            CameraC
        }

        void Start()
        {
            SetInitialCameraState();

            // Desactivar scripts desde el inicio
            if (scriptsToEnable != null)
                foreach (var script in scriptsToEnable)
                    if (script != null)
                        script.enabled = false;

            // Canvas inicial
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                if (extraCanvas != null)
                    extraCanvas.gameObject.SetActive(false);
                StartCoroutine(FadeOutAfterDelay());
            }

            // Desactivar Animator al inicio
            if (targetAnimator != null)
                targetAnimator.enabled = false;
        }

        void Update()
        {
            if (ragdollHanger == null)
                return;

            // ⭐ SALTAR TODA LA CINEMÁTICA CON ENTER
            if (Input.GetKeyDown(KeyCode.Return)) 
            {
                StartCoroutine(SkipToCameraC());
                return;
            }

            HandleCameraTransitions();
            HandleCanvasLogic();
            HandleAnimatorLogic();
        }

        #region === SKIP DIRECTO A CÁMARA C ===
        private IEnumerator SkipToCameraC()
        {
            // Forzar prioridad INMEDIATA a cámara C
            foreach (var vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
                vcam.Priority = (vcam == cameraC) ? 100 : 0;

            // Activar objeto de cámara C inmediatamente
            if (objectC) objectC.SetActive(true);

            // Apagar cámaras A y B instantáneamente
            if (objectA) objectA.SetActive(false);
            if (objectB) objectB.SetActive(false);

            // Apagar main camera si corresponde
            if (mainCameraToDisable != null)
                mainCameraToDisable.SetActive(false);

            // Activar scripts del player al instante
            if (scriptsToEnable != null)
                foreach (var script in scriptsToEnable)
                    if (script != null)
                        script.enabled = true;

            // 🔥 Apagar este GameObject inmediatamente
            gameObject.SetActive(false);

            yield break;
        }
        #endregion

        #region === CÁMARA ===
        private void SetInitialCameraState()
        {
            if (objectA) objectA.SetActive(true);
            if (objectB) objectB.SetActive(false);
            if (objectC) objectC.SetActive(false);

            if (cameraA) cameraA.gameObject.SetActive(true);
            if (cameraB) cameraB.gameObject.SetActive(true);
            if (cameraC) cameraC.gameObject.SetActive(true);

            switch (startCamera)
            {
                case StartCamera.CameraA: SetPriority(cameraA); break;
                case StartCamera.CameraB: SetPriority(cameraB); break;
                case StartCamera.CameraC: SetPriority(cameraC); break;
            }
        }

        private void SetPriority(CinemachineVirtualCameraBase activeCam)
        {
            foreach (var vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
                vcam.Priority = (vcam == activeCam) ? 20 : 5;
        }

        private void HandleCameraTransitions()
        {
            if (transitionToA && !prevA)
                StartCoroutine(SwitchRoutine(cameraA, "A"));
            if (transitionToB && !prevB)
                StartCoroutine(SwitchRoutine(cameraB, "B"));
            if (transitionToC && !prevC)
                StartCoroutine(SwitchRoutine(cameraC, "C"));

            prevA = transitionToA;
            prevB = transitionToB;
            prevC = transitionToC;

            if (ragdollHanger.fadeBlack && !fadeBlackPrev)
                StartCoroutine(SwitchRoutine(cameraB, "B"));

            fadeBlackPrev = ragdollHanger.fadeBlack;
        }

        private IEnumerator SwitchRoutine(CinemachineVirtualCameraBase newCam, string targetCamera)
        {
            if (isTransitioning) yield break;
            isTransitioning = true;

            // Activar destino
            if (targetCamera == "B" && objectB) objectB.SetActive(true);
            if (targetCamera == "C" && objectC) objectC.SetActive(true);

            foreach (var vcam in FindObjectsOfType<CinemachineVirtualCameraBase>())
                vcam.Priority = (vcam == newCam) ? 20 : 5;

            yield return new WaitForSeconds(blendTime);

            if (targetCamera == "B")
            {
                if (objectA) objectA.SetActive(false);
                if (objectToDeactivate) objectToDeactivate.SetActive(false);
            }
            else if (targetCamera == "C")
            {
                if (objectB) objectB.SetActive(false);

                if (mainCameraToDisable != null)
                    mainCameraToDisable.SetActive(false);

                if (scriptsToEnable != null && scriptsToEnable.Length > 0)
                    StartCoroutine(ActivateScriptsWithDelay());
            }

            isTransitioning = false;
        }

        private IEnumerator ActivateScriptsWithDelay()
        {
            yield return new WaitForSeconds(scriptsActivationDelay);

            foreach (var script in scriptsToEnable)
                if (script != null)
                    script.enabled = true;
        }
        #endregion

        #region === CANVAS ===
        private void HandleCanvasLogic()
        {
            if (canvasGroup == null)
                return;

            if (ragdollHanger.fadeBlack && extraCanvas != null && extraCanvas.gameObject.activeSelf)
                extraCanvas.gameObject.SetActive(false);

            if (ragdollHanger.fadeBlack && !hasReappeared)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                hasReappeared = true;
                StartCoroutine(FadeOutAfterFadeBlack());
            }

            if (ragdollHanger.hasReleased && !hasActivatedExtraCanvas && extraCanvas != null)
            {
                hasActivatedExtraCanvas = true;
                StartCoroutine(ActivateExtraCanvas());
            }
        }

        private IEnumerator FadeOutAfterDelay()
        {
            yield return new WaitForSeconds(startFadeDelay);
            hasStartedFade = true;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < startFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / startFadeDuration;
                canvasGroup.alpha = Mathf.SmoothStep(startAlpha, 0f, t);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private IEnumerator FadeOutAfterFadeBlack()
        {
            yield return new WaitForSeconds(fadeBlackDelay);

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;

            while (elapsed < fadeBlackDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeBlackDuration;
                canvasGroup.alpha = Mathf.SmoothStep(startAlpha, 0f, t);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            hasFadedAfterBlack = true;
        }

        private IEnumerator ActivateExtraCanvas()
        {
            extraCanvas.gameObject.SetActive(true);
            yield return new WaitForSeconds(extraCanvasActiveTime);

            if (extraCanvas != null)
                extraCanvas.gameObject.SetActive(false);
        }
        #endregion

        #region === ANIMATOR ===
        private void HandleAnimatorLogic()
        {
            if (targetAnimator == null || animatorStarted)
                return;

            if (ragdollHanger.fadeBlack)
            {
                StartCoroutine(StartAnimatorAfterDelay());
                animatorStarted = true;
            }
        }

        private IEnumerator StartAnimatorAfterDelay()
        {
            yield return new WaitForSeconds(animatorStartDelay);

            if (targetAnimator != null)
            {
                targetAnimator.enabled = true;
                animatorActivated = true;
            }

            yield return new WaitForSeconds(transitionAfterAnimatorDelay);
            if (cameraC != null)
                StartCoroutine(SwitchRoutine(cameraC, "C"));
        }
        #endregion
    }
}







