using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Para Volume (URP/HDRP)

namespace UIScripts.UI_Pause_Menu
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/Menu Manager")]
    public class MenuManager : MonoBehaviour
    {
        [Serializable]
        public struct MenuPanel
        {
            [Tooltip("ID único para referenciar este panel, ej: 'Pause', 'Settings', 'Inventory'")]
            public string id;
            [Tooltip("GameObject raíz del Canvas/Panel")]
            public GameObject panel;
            [Tooltip("Opcional: Panel inicia activo")]
            public bool startActive;
        }

        [Header("Paneles Registrados")]
        [SerializeField] private List<MenuPanel> panels = new List<MenuPanel>();

        [Header("ID del Panel de Pausa")]
        [SerializeField] private string pausePanelId = "Pause";

        [Header("Transición Suave")]
        [Tooltip("CanvasGroup del panel de pausa (para fade). Recomendado: el root del panel).")]
        [SerializeField] private CanvasGroup pauseCanvasGroup;
        [Tooltip("Overlay opcional para atenuar el fondo (Image negro con alpha bajo).")]
        [SerializeField] private Image worldDimmer;
        [Range(0f, 0.9f)] [SerializeField] private float worldDimmerMaxAlpha = 0.35f;

        [Tooltip("Duración de la transición de abrir/cerrar (segundos, unscaled).")]
        [SerializeField] private float transitionDuration = 0.25f;
        [Tooltip("Curva de la transición (0→1).")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Time Scale (opcional)")]
        [Tooltip("Suavizar el timeScale 1→0 (al pausar) y 0→1 (al reanudar).")]
        [SerializeField] private bool smoothTimeScale = true;
        [Tooltip("Umbral de opacidad a partir del cual se 'consolida' el estado de pausa.")]
        [Range(0.1f, 0.9f)] [SerializeField] private float pauseCommitThreshold = 0.6f;

        [Header("Auto-Focus (Gamepad/Teclado)")]
        [Tooltip("Primer botón/elemento que quedará seleccionado al terminar de pausar.")]
        [SerializeField] private GameObject firstSelected;

        [Header("Post-Proceso (URP/HDRP)")]
        [Tooltip("Volume a mezclar durante la transición (ej. DoF/Bloom/ColorAdjustments).")]
        [SerializeField] private Volume volumeToBlend;
        [Tooltip("Peso objetivo del Volume al 100% de pausa.")]
        [Range(0f, 1f)] [SerializeField] private float volumeMaxWeight = 0.8f;

        [Header("Control de entrada")]
        [Tooltip("Usar Input System (si está habilitado) además de la tecla Escape")]
        [SerializeField] private bool useNewInputSystem = true;
#if ENABLE_INPUT_SYSTEM
    [Tooltip("Action que dispara la pausa (ej: <Keyboard>/escape)")]
    [SerializeField] private InputActionReference pauseAction;
#endif

        [Header("Eventos")]
        public UnityEvent OnPaused;   // Transición de pausa terminada
        public UnityEvent OnResumed;  // Transición de reanudar terminada

        private readonly Dictionary<string, GameObject> _dict = new();
        private bool _isPaused;
        private bool _isTransitioning;
        private Coroutine _transitionCo;

        void Awake()
        {
            _dict.Clear();
            foreach (var p in panels)
            {
                if (string.IsNullOrWhiteSpace(p.id) || p.panel == null) continue;
                if (!_dict.ContainsKey(p.id)) _dict.Add(p.id, p.panel);
                p.panel.SetActive(p.startActive);
            }

            // Estado inicial no pausado
            ApplyImmediatePauseVisuals(false);
            ApplyVolumeWeight(0f);
            SetTimescaleImmediate(1f);
            AudioListener.pause = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            _isPaused = false;
        }

        void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
        if (useNewInputSystem && pauseAction != null)
        {
            pauseAction.action.performed += OnPausePerformed;
            pauseAction.action.Enable();
        }
#endif
        }

        void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
        if (useNewInputSystem && pauseAction != null)
        {
            pauseAction.action.performed -= OnPausePerformed;
            pauseAction.action.Disable();
        }
#endif
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                TogglePause();
        }

#if ENABLE_INPUT_SYSTEM
    private void OnPausePerformed(InputAction.CallbackContext ctx) => TogglePause();
#endif

        public void TogglePause()
        {
            if (_isTransitioning) return;

            if (!_dict.ContainsKey(pausePanelId))
            {
                Debug.LogWarning($"[MenuManager] No se encontró panel con id '{pausePanelId}'.");
                return;
            }

            if (_isPaused) StartResumeSequence();
            else StartPauseSequence();
        }

        public void Pause()
        {
            if (_isPaused || _isTransitioning) return;
            StartPauseSequence();
        }

        public void Resume()
        {
            if (!_isPaused || _isTransitioning) return;
            StartResumeSequence();
        }

        // ------- Transiciones -------
        private void StartPauseSequence()
        {
            ShowOnly(pausePanelId);
            ApplyImmediatePauseVisuals(false); // alpha 0 al inicio
            ApplyVolumeWeight(0f);

            if (_transitionCo != null) StopCoroutine(_transitionCo);
            _transitionCo = StartCoroutine(Co_Transition(true));
        }

        private void StartResumeSequence()
        {
            if (_transitionCo != null) StopCoroutine(_transitionCo);
            _transitionCo = StartCoroutine(Co_Transition(false));
        }

        private IEnumerator Co_Transition(bool toPaused)
        {
            _isTransitioning = true;

            float t = 0f;
            float startA = pauseCanvasGroup ? pauseCanvasGroup.alpha : (toPaused ? 0f : 1f);
            float endA   = toPaused ? 1f : 0f;
            bool committedPause = false;

            // Limpiar selección antes de animar
            EventSystem.current?.SetSelectedGameObject(null);

            while (t < transitionDuration)
            {
                t += Time.unscaledDeltaTime; // SIEMPRE tiempo no escalado
                float k = Mathf.Clamp01(t / transitionDuration);
                float e = transitionCurve.Evaluate(k);

                float a = Mathf.Lerp(startA, endA, e);
                ApplyVisualsAlpha(a);
                ApplyVolumeWeight(a);

                // Smooth timeScale (opcional)
                if (smoothTimeScale)
                    SetTimescaleImmediate(1f - a); // a: 0→1 => ts: 1→0

                // Consolidar estado de pausa
                if (toPaused && !committedPause && a >= pauseCommitThreshold)
                {
                    committedPause = true;
                    _isPaused = true;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    AudioListener.pause = true;
                    if (!smoothTimeScale) SetTimescaleImmediate(0f);
                }

                yield return null;
            }

            // Estado final + eventos
            ApplyVisualsAlpha(endA);
            ApplyVolumeWeight(endA);

            if (toPaused)
            {
                _isPaused = true;
                if (!committedPause)
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                    AudioListener.pause = true;
                    if (!smoothTimeScale) SetTimescaleImmediate(0f);
                }

                // Auto-focus del primer botón (si existe)
                if (firstSelected != null && firstSelected.activeInHierarchy)
                    EventSystem.current?.SetSelectedGameObject(firstSelected);

                OnPaused?.Invoke();
            }
            else
            {
                _isPaused = false;
                HideAll();
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                AudioListener.pause = false;
                if (!smoothTimeScale) SetTimescaleImmediate(1f);

                // Limpiar selección al salir
                EventSystem.current?.SetSelectedGameObject(null);

                OnResumed?.Invoke();
            }

            _isTransitioning = false;
            _transitionCo = null;
        }

        // ------- Visuales -------
        private void ApplyVisualsAlpha(float a)
        {
            if (pauseCanvasGroup)
            {
                pauseCanvasGroup.alpha = a;
                pauseCanvasGroup.blocksRaycasts = a > 0.001f;
                pauseCanvasGroup.interactable = a > 0.999f;
            }
            if (worldDimmer)
            {
                var c = worldDimmer.color;
                c.a = worldDimmerMaxAlpha * a;
                worldDimmer.color = c;
            }
        }

        private void ApplyImmediatePauseVisuals(bool paused)
        {
            float a = paused ? 1f : 0f;
            if (pauseCanvasGroup)
            {
                pauseCanvasGroup.alpha = a;
                pauseCanvasGroup.blocksRaycasts = paused;
                pauseCanvasGroup.interactable = paused;
            }
            if (worldDimmer)
            {
                var c = worldDimmer.color;
                c.a = worldDimmerMaxAlpha * a;
                worldDimmer.color = c;
            }
        }

        private void ApplyVolumeWeight(float a01)
        {
            if (volumeToBlend != null)
                volumeToBlend.weight = Mathf.Clamp01(a01) * volumeMaxWeight;
        }

        private void SetTimescaleImmediate(float v)
        {
            Time.timeScale = Mathf.Clamp01(v);
        }

        // ------- Utilidades de panel -------
        public void ShowOnly(string id)
        {
            foreach (var kv in _dict)
                kv.Value.SetActive(kv.Key == id);
        }

        public void Show(string id)
        {
            if (_dict.TryGetValue(id, out var go)) go.SetActive(true);
        }

        public void Hide(string id)
        {
            if (_dict.TryGetValue(id, out var go)) go.SetActive(false);
        }

        public void HideAll()
        {
            foreach (var kv in _dict) kv.Value.SetActive(false);
        }

        public bool IsPaused() => _isPaused;
    }
}
