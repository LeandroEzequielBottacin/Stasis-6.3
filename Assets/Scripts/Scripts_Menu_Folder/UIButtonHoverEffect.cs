using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Scripts_Menu_Folder
{
    [DisallowMultipleComponent]
    public class UIButtonHoverEffectTMP : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("TextMeshPro Settings")]
        public TMP_Text buttonText;
        public Color hoverColor = Color.yellow;
        public float hoverFontSize = 36f;
        public float transitionSpeed = 5f;

        [Header("Transform Hover Settings")]
        [Tooltip("Si se deja vacío, usa el RectTransform del mismo objeto.")]
        public RectTransform targetTransform;
        [Tooltip("Escala cuando se hace hover (1 = sin cambio).")]
        public Vector3 hoverScale = new Vector3(1.05f, 1.05f, 1f);

        [Header("Typography Hover Settings")]
        [Tooltip("¿Usar bold cuando el mouse está encima?")]
        public bool useBoldOnHover = true;
        [Tooltip("Espaciado de caracteres al hacer hover.")]
        public float hoverCharacterSpacing = 5f;

        [Header("Audio Settings")]
        public AudioSource audioSource;
        public AudioClip hoverSound;
        public AudioClip clickSound;

        [Header("Audio Rate Limiting")]
        [Tooltip("Tiempo mínimo entre sonidos de hover, en segundos. Es compartido por todos los botones.")]
        [SerializeField] private float hoverSoundMinInterval = 0.08f;

        // Cooldown GLOBAL para todos los botones que usen este script
        private static float LastHoverSoundTime { get; set; } = -999f;

        // Estado original
        private Color _originalColor;
        private float _originalFontSize;
        private float _originalCharacterSpacing;
        private Vector3 _originalScale;
        private FontStyles _originalFontStyle;

        private bool _isHovered;
        private bool _isClickAnimating;

        private void Start()
        {
            if (buttonText == null)
                buttonText = GetComponentInChildren<TMP_Text>();

            if (buttonText == null)
            {
                Debug.LogError("UIButtonHoverEffectTMP: No TMP_Text found in children.");
                enabled = false;
                return;
            }

            if (targetTransform == null)
                targetTransform = GetComponent<RectTransform>();

            if (targetTransform == null)
            {
                Debug.LogWarning("UIButtonHoverEffectTMP: No RectTransform found. Desactivando efectos de escala.");
            }

            // Cacheamos valores originales
            _originalColor = buttonText.color;
            _originalFontSize = buttonText.fontSize;
            _originalCharacterSpacing = buttonText.characterSpacing;
            _originalFontStyle = buttonText.fontStyle;

            if (targetTransform != null)
                _originalScale = targetTransform.localScale;

            if (audioSource == null)
                Debug.LogWarning("UIButtonHoverEffectTMP: No AudioSource assigned.");
        }

        private void Update()
        {
            var deltaTime = Time.unscaledDeltaTime;
            var lerpT = deltaTime * transitionSpeed;

            // Color + tamaño de fuente
            if (_isHovered)
            {
                buttonText.color = Color.Lerp(buttonText.color, hoverColor, lerpT);
                buttonText.fontSize = Mathf.Lerp(buttonText.fontSize, hoverFontSize, lerpT);

                // Espaciado de caracteres
                buttonText.characterSpacing = Mathf.Lerp(
                    buttonText.characterSpacing,
                    hoverCharacterSpacing,
                    lerpT
                );

                // Bold
                if (useBoldOnHover)
                    buttonText.fontStyle = _originalFontStyle | FontStyles.Bold;
            }
            else
            {
                buttonText.color = Color.Lerp(buttonText.color, _originalColor, lerpT);
                buttonText.fontSize = Mathf.Lerp(buttonText.fontSize, _originalFontSize, lerpT);

                buttonText.characterSpacing = Mathf.Lerp(
                    buttonText.characterSpacing,
                    _originalCharacterSpacing,
                    lerpT
                );

                // Volver al estilo original
                buttonText.fontStyle = _originalFontStyle;
            }

            // Escala de transform
            if (!targetTransform) return;
            var targetScale = _isHovered ? hoverScale : _originalScale;
            targetTransform.localScale = Vector3.Lerp(targetTransform.localScale, targetScale, lerpT);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;

            // Sonido con cooldown global
            if (hoverSound == null || audioSource == null) return;
            var now = Time.unscaledTime;
            if (!(now - LastHoverSoundTime >= hoverSoundMinInterval)) return;
            // Opcional: pequeña variación de pitch para que no sea tan monótono
            // audioSource.pitch = Random.Range(0.97f, 1.03f);

            audioSource.PlayOneShot(hoverSound);
            LastHoverSoundTime = now;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (clickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(clickSound);
            }

            if (!_isClickAnimating && targetTransform != null)
                StartCoroutine(ClickPulseRoutine());
        }

        /// <summary>
        /// Pequeña animación de pulsación en el click (scale down y vuelta).
        /// </summary>
        private IEnumerator ClickPulseRoutine()
        {
            _isClickAnimating = true;

            const float durationDown = 0.06f;
            const float durationUp = 0.10f;

            // Partimos del estado actual (por si está en hover)
            var startScale = targetTransform.localScale;
            var downScale = startScale * 0.94f; // un poco más chico

            var t = 0f;

            // Fase 1: escala hacia abajo
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / durationDown;
                var k = Mathf.Clamp01(t);
                targetTransform.localScale = Vector3.Lerp(startScale, downScale, k);
                yield return null;
            }

            // Fase 2: vuelve a la escala objetivo (hover u original)
            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / durationUp;
                var k = Mathf.Clamp01(t);

                var targetScale = _isHovered ? hoverScale : _originalScale;
                targetTransform.localScale = Vector3.Lerp(downScale, targetScale, k);

                yield return null;
            }

            _isClickAnimating = false;
        }
    }
}
