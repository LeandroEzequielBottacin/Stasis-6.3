using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace UIScripts.UI_Reboot
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Reboot Sequence Controller")]
    public class RebootSequenceController : MonoBehaviour
    {
        [Header("Asset (arrastrá o usa ResourcesPath)")]
        [SerializeField] private BootSequenceAsset asset;
        [Tooltip("Opcional: si 'asset' está vacío, intenta cargar desde Resources con esta ruta (sin .asset). Ej: UI/Boot/BootSequence_Default")]
        [SerializeField] private string resourcesPath;

        [Header("UI (autobind si quedan vacíos)")]
        [SerializeField] private TextMeshProUGUI consoleText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Control")]
        [SerializeField] private KeyCode skipKey = KeyCode.Space;
        [SerializeField] private bool startOnAwake = true;

        [Header("Audio (autobind si queda vacío)")]
        [SerializeField] private AudioSource audioSource;

        [Header("Events")]
        public UnityEvent onSequenceStart;
        public UnityEvent onSequenceEnd;

        [Header("Fade final de texto")]
        [SerializeField] private bool fadeTextOnEnd = true;
        [SerializeField] private float endFadeDelay = 4f;
        [SerializeField] private float endFadeDuration = 1.25f;
        [SerializeField] private bool clearTextOnEnd = true;
        [SerializeField] private bool deactivateTextGOOnEnd = false;

        [Header("Persistencia de texto")]
        [Tooltip("Si está activo, el texto no se limpia ni se hace fade/clear al final. Todo lo escrito permanece en pantalla.")]
        [SerializeField] private bool keepTextPersistent = false;

        private bool _skipping;

        private void Reset()
        {
            TryAutoBind(true);
        }

        private void Awake()
        {
            if (asset == null && !string.IsNullOrWhiteSpace(resourcesPath))
            {
                asset = UnityEngine.Resources.Load<BootSequenceAsset>(resourcesPath);
                if (asset == null)
                    Debug.LogWarning($"[RebootSequenceController] No se encontró BootSequenceAsset en Resources: '{resourcesPath}'.");
            }

            TryAutoBind(false);

            if (canvasGroup != null) canvasGroup.alpha = 1f;

            if (consoleText != null)
            {
                // Si NO queremos persistencia, limpiamos el texto al inicio
                if (!keepTextPersistent)
                    consoleText.text = string.Empty;

                consoleText.alpha = 1f;
                consoleText.canvasRenderer.SetAlpha(1f);
            }
        }

        private void Start()
        {
            if (!ValidateReady()) return;
            if (startOnAwake) StartSequence();
        }

        // ---------- PUBLIC ----------
        public void SetAsset(BootSequenceAsset newAsset) => asset = newAsset;

        public void StartSequence()
        {
            if (!ValidateReady()) return;
            StopAllCoroutines();
            StartCoroutine(RunSequence());
        }

        /// <summary>
        /// Permite activar la secuencia desde otros scripts, botones o eventos.
        /// </summary>
        public void Activate()
        {
            StartSequence();
        }

        // ---------- CORE ----------
        private IEnumerator RunSequence()
        {
            onSequenceStart?.Invoke();

            if (consoleText != null)
            {
                // Si NO queremos persistencia, limpiamos al inicio de la secuencia.
                // Si SÍ queremos persistencia, dejamos lo que ya hay y opcionalmente agregamos un salto de línea.
                if (!keepTextPersistent)
                {
                    consoleText.text = string.Empty;
                }
                else if (!string.IsNullOrEmpty(consoleText.text))
                {
                    consoleText.text += "\n";
                }

                consoleText.alpha = 1f;
                consoleText.canvasRenderer.SetAlpha(1f);
            }

            foreach (var step in asset.steps)
            {
                float typing = step.typingSpeed > 0 ? step.typingSpeed : asset.defaultTypingSpeed;
                float delay = step.afterDelay > 0 ? step.afterDelay : asset.defaultAfterDelay;

                yield return StartCoroutine(TypeLine(step.text, typing, step.playTypeBeep));

                if (step.playLineBeep) Play(asset.lineBeep);

                yield return StartCoroutine(DelayOrSkip(delay));
            }

            Play(asset.finishSfx);

            // Si NO queremos persistencia, aplicamos el comportamiento de fade/clear
            if (!keepTextPersistent && fadeTextOnEnd && consoleText != null)
            {
                yield return StartCoroutine(DelayOrSkip(endFadeDelay));
                yield return StartCoroutine(FadeTMPText(consoleText, 1f, 0f, endFadeDuration));

                if (clearTextOnEnd) consoleText.text = string.Empty;
                if (deactivateTextGOOnEnd) consoleText.gameObject.SetActive(false);
            }

            // Si NO queremos persistencia, respetamos el autoFadeOut del CanvasGroup
            if (!keepTextPersistent && asset.autoFadeOut && canvasGroup != null)
            {
                yield return StartCoroutine(DelayOrSkip(asset.fadeDelay));
                yield return StartCoroutine(FadeCanvas(canvasGroup, 1f, 0f, asset.fadeDuration));
            }

            onSequenceEnd?.Invoke();

            // Si queremos persistencia, NO desactivamos el CanvasGroup ni el GameObject,
            // para que el texto persista en pantalla.
            if (!keepTextPersistent)
            {
                if (canvasGroup != null)
                    canvasGroup.gameObject.SetActive(false);
                else
                    gameObject.SetActive(false);
            }
        }

        private IEnumerator TypeLine(string line, float typingSpeed, bool beepPerChar)
        {
            if (consoleText == null)
                yield break;

            for (int i = 0; i < line.Length; i++)
            {
                if (_skipping)
                {
                    consoleText.text += line.Substring(i);
                    break;
                }

                consoleText.text += line[i];

                if (beepPerChar && (i % 2 == 0)) Play(asset.typeBeep);

                float elapsed = 0f;
                while (elapsed < typingSpeed && !_skipping)
                {
                    if (Input.GetKey(skipKey)) _skipping = true;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            consoleText.text += "\n";
            _skipping = false;
        }

        private IEnumerator DelayOrSkip(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (Input.GetKey(skipKey)) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _skipping = false;
        }

        private IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float duration)
        {
            float t = 0f;
            cg.alpha = from;
            while (t < duration)
            {
                if (Input.GetKey(skipKey)) { cg.alpha = to; break; }
                t += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            cg.alpha = to;
        }

        private IEnumerator FadeTMPText(TextMeshProUGUI tmp, float from, float to, float duration)
        {
            if (tmp == null) yield break;

            tmp.alpha = 1f;
            tmp.canvasRenderer.SetAlpha(from);

            if (duration <= 0f)
            {
                tmp.canvasRenderer.SetAlpha(to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                if (Input.GetKey(skipKey)) { tmp.canvasRenderer.SetAlpha(to); break; }
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(from, to, t / duration);
                tmp.canvasRenderer.SetAlpha(a);
                yield return null;
            }

            tmp.canvasRenderer.SetAlpha(to);
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        }

        private void TryAutoBind(bool inReset)
        {
            if (consoleText == null)
            {
                consoleText = GetComponentInChildren<TextMeshProUGUI>(true);
                if (!inReset && consoleText == null)
                    Debug.LogWarning("[RebootSequenceController] No se encontró TextMeshProUGUI hijo.");
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = GetComponentInChildren<CanvasGroup>(true);

                if (!inReset && canvasGroup == null)
                    Debug.LogWarning("[RebootSequenceController] No se encontró CanvasGroup.");
            }

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = GetComponentInChildren<AudioSource>(true);
            }
        }

        private bool ValidateReady()
        {
            if (asset == null)
            {
                Debug.LogError("[RebootSequenceController] 'asset' no asignado.");
                return false;
            }
            if (consoleText == null)
            {
                Debug.LogError("[RebootSequenceController] 'consoleText' no asignado.");
                return false;
            }
            return true;
        }
    }
}
