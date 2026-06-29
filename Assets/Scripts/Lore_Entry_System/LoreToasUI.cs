using TMPro;
using UnityEngine;

namespace Lore_Entry_System
{
    [DisallowMultipleComponent]
    public class LoreToastUI : MonoBehaviour
    {
        [Header("Refs (UI)")]
        [Tooltip("CanvasGroup del cartel/hint.")]
        public CanvasGroup group;

        [Tooltip("Texto (TMP) del hint. Ej: 'Press I to read new entry'")]
        public TMP_Text message;

        [Tooltip("Duración (seg) visible antes de ocultar (0 = no autohide).")]
        public float autoHideSeconds = 0f;

        [Header("Texto por defecto")]
        [SerializeField] private string defaultText = "Press I to read new entry";

        [Header("Opciones")]
        [Tooltip("Si el GO está desactivado en jerarquía, se activará al mostrar.")]
        public bool ensureActiveOnShow = true;

        private float _tHide = -1f;
        private LoreSystem _system;

        // ================== Lifecycle ==================
        void Awake()
        {
            if (!group) group = GetComponentInChildren<CanvasGroup>(true);
            HideImmediate(); // arrancar oculto por alpha

            _system = FindFirstObjectByType<LoreSystem>();
            if (_system) _system.OnEntryUnlocked += OnEntryUnlocked;
            else Debug.LogWarning("LoreToastUI: No se encontró LoreSystem en escena.");
        }

        void OnEnable()
        {
            // Reafirma que arranca oculto si el prefab tenía alpha 1
            HideImmediate();
        }

        void OnDestroy()
        {
            if (_system) _system.OnEntryUnlocked -= OnEntryUnlocked;
        }

        // ================== Callbacks ==================
        private void OnEntryUnlocked(LoreEntry e)
        {
            // Construir texto (usa título si está disponible)
            var txt = defaultText;
            if (e != null && !string.IsNullOrEmpty(e.title))
                txt = $"Press I to read: {e.title}";

            Show(txt, autoHideSeconds > 0f ? autoHideSeconds : -1f);
        }

        void Update()
        {
            if (_tHide > 0f && Time.unscaledTime >= _tHide)
            {
                _tHide = -1f;
                Hide();
            }
        }

        // ================== API pública ==================
        /// <summary>
        /// Mostrar usando texto por defecto y sin forzar autohide (ideal para UnityEvent).
        /// </summary>
        public void Show()  // llamado desde UnityEvent
        {
            // Usar autoHideSeconds si > 0, sino queda fijo (sin autohide)
            Show(null, autoHideSeconds > 0f ? autoHideSeconds : -1f);
        }


        /// <summary>
        /// Mostrar con texto opcional y duración (seg). seconds &lt;= 0 ⇒ sin autohide.
        /// </summary>
        public void Show(string customText, float seconds)
        {
            if (message)
            {
                message.text = string.IsNullOrEmpty(customText) ? defaultText : customText;
            }

            if (ensureActiveOnShow && !gameObject.activeInHierarchy)
                gameObject.SetActive(true);

            SetAlpha(1f);

            _tHide = (seconds > 0f) ? (Time.unscaledTime + seconds) : -1f;
        }

        /// <summary>Ocultar inmediatamente.</summary>
        public void Hide()
        {
            SetAlpha(0f);
        }

        // ================== Internos ==================
        private void HideImmediate()
        {
            SetAlpha(0f);
        }

        private void SetAlpha(float a)
        {
            if (!group) return;
            group.alpha = a;
            // No hace falta interactuable/raycast para un hint; igual los seteamos coherentes.
            bool on = a > 0.99f;
            group.interactable = on;
            group.blocksRaycasts = on;
        }
    }
}
