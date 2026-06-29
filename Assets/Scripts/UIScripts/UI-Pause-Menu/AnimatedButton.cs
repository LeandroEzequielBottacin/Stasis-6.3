using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UIScripts.UI_Pause_Menu
{
    // Direcciones de entrada
    public enum EntryDirection { Left, Right, Top, Bottom, Scale }

    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(AudioSource))]
    public class AnimatedButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("General")]
        [Tooltip("Usar tiempo no escalado (recomendado si el juego está en pausa).")]
        public bool useUnscaledTime = true;

        [Header("Entry Animation")]
        public EntryDirection entryDirection = EntryDirection.Scale;
        public float entryDuration = 0.5f;
        public float entryDelay;
        public Vector2 offset = new Vector2(100, 0);
        [Tooltip("Easing al entrar moviendo.")]
        public Ease entryMoveEase = Ease.OutCubic;
        [Tooltip("Easing al entrar por escala.")]
        public Ease entryScaleEase = Ease.OutBack;

        [Header("Hover Animation")]
        public bool enableHover = true;
        public float hoverScale = 1.1f;
        public float hoverDuration = 0.2f;
        public Ease hoverEase = Ease.OutBack;
        public bool hoverSound;
        public AudioClip hoverClip;

        [Header("Click Animation")]
        public bool enableClick = true;
        [Tooltip("Escala objetivo al hacer click (ej. 0.9 = 90%).")]
        public float clickScale = 0.9f;
        public float clickDuration = 0.1f;
        public Ease clickEase = Ease.OutFlash;
        public bool clickSound;
        public AudioClip clickClip;

        private RectTransform rect;
        private AudioSource audioSource;
        private Vector2 originalAnchoredPos;
        private Vector3 originalScale;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            audioSource = GetComponent<AudioSource>();
            originalAnchoredPos = rect.anchoredPosition;
            originalScale = rect.localScale;
        }

        void OnEnable()
        {
            PlayEntry();
        }

        // Ejecuta la animación de entrada
        public void PlayEntry()
        {
            rect.DOKill();

            if (entryDirection == EntryDirection.Scale)
            {
                rect.localScale = Vector3.zero;
                rect.anchoredPosition = originalAnchoredPos;
            }
            else
            {
                rect.localScale = originalScale;
                rect.anchoredPosition = originalAnchoredPos + OffsetForDirection();
            }

            Sequence seq = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject);

            if (entryDelay > 0f) seq.AppendInterval(entryDelay);

            if (entryDirection == EntryDirection.Scale)
            {
                seq.Append(
                    rect.DOScale(originalScale, entryDuration)
                        .SetEase(entryScaleEase)
                        .SetUpdate(useUnscaledTime)
                        .SetLink(gameObject)
                );
            }
            else
            {
                seq.Append(
                    rect.DOAnchorPos(originalAnchoredPos, entryDuration)
                        .SetEase(entryMoveEase)
                        .SetUpdate(useUnscaledTime)
                        .SetLink(gameObject)
                );
            }
        }

        // Calcula el offset inicial según la dirección
        Vector2 OffsetForDirection()
        {
            switch (entryDirection)
            {
                case EntryDirection.Left:   return new Vector2(-offset.x, 0);
                case EntryDirection.Right:  return new Vector2( offset.x, 0);
                case EntryDirection.Top:    return new Vector2(0,  offset.y);
                case EntryDirection.Bottom: return new Vector2(0, -offset.y);
                default: return Vector2.zero;
            }
        }

        // Hover
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enableHover) return;
            rect.DOKill();
            rect.DOScale(originalScale * hoverScale, hoverDuration)
                .SetEase(hoverEase)
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject);

            if (hoverSound && hoverClip && audioSource)
                audioSource.PlayOneShot(hoverClip);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!enableHover) return;
            rect.DOKill();
            rect.DOScale(originalScale, hoverDuration)
                .SetEase(hoverEase)
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject);
        }

        // Click
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!enableClick) return;
            rect.DOKill();

            // punch = (escalaObjetivo - escalaBase)
            Vector3 targetScale = originalScale * Mathf.Max(0.0001f, clickScale);
            Vector3 punch = targetScale - originalScale;

            rect.DOPunchScale(punch, clickDuration)
                .SetEase(clickEase)
                .SetUpdate(useUnscaledTime)
                .SetLink(gameObject);

            if (clickSound && clickClip && audioSource)
                audioSource.PlayOneShot(clickClip);
        }
    }
}