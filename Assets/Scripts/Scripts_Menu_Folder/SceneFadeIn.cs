using System.Collections;
using UnityEngine;

namespace Scripts_Menu_Folder
{
    public class SceneFadeIn : MonoBehaviour
    {
        [Header("Black Screen Settings")]
        [Tooltip("Canvas o GameObject con el BlackScreen")]
        public GameObject blackScreen;

        [Header("Fade Settings")]
        [Tooltip("Duración total del fade en segundos")]
        public float fadeDuration = 1.5f;

        [Tooltip("Velocidad de cambio de alfa")]
        public float fadeSpeed = 1.0f;

        private CanvasGroup canvasGroup;

        void Awake()
        {
            if (blackScreen == null)
            {
                Debug.LogError("No se asignó el BlackScreen en " + gameObject.name);
                return;
            }

            canvasGroup = blackScreen.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = blackScreen.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true; 
        }

        void Start()
        {
            StartCoroutine(FadeIn());
        }

        IEnumerator FadeIn()
        {
            float elapsedTime = 0f;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime * fadeSpeed;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false; 
        }
    }
}

