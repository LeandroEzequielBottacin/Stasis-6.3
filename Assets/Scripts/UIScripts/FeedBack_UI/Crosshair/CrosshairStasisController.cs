using System;
using Player.Stasis;
using UnityEngine;
using UnityEngine.UI;

namespace UIScripts.FeedBack_UI.Crosshair
{
    /// <summary>
    /// Crosshair reactivo con ease, pulso y overlay opcional.
    /// - Raycastea solo contra layer de proxies (StasisProxy).
    /// - Emite OnAcquireTarget / OnLoseTarget.
    /// - Pulso respirante permanente (más marcado con target).
    /// </summary>
    [AddComponentMenu("Scripts/UIScripts/FeedBack_UI/Crosshair")]
    public class CrosshairStasisController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Cámara que dispara el raycast. Si null, usa Camera.main.")]
        public Camera cam;

        [Tooltip("UI Image del crosshair principal.")]
        public Image crosshairImage;

        [Tooltip("RectTransform del crosshair (si null, usa el de la Image).")]
        public RectTransform crosshairRect;

        [Header("Raycast (solo proxies)")]
        public float maxDistance = 40f;
        [Tooltip("LayerMask de proxies (ej. solo 'StasisProxy').")]
        public LayerMask proxyLayerMask;
        [Tooltip("Usar 'Collide' para impactar proxies en Trigger.")]
        public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Look & Feel - Color")]
        public Color baseColor = new Color(1f, 1f, 1f, 0.9f);
        public Color stasisColor = new Color(0.3f, 1f, 0.4f, 1f);
        [Tooltip("Curva de mezcla de color (0=base, 1=stasis).")]
        public AnimationCurve colorEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Look & Feel - Escala")]
        [Tooltip("Escala base (1 = 100%).")]
        public float baseScale = 1f;
        [Tooltip("Escala al adquirir target.")]
        public float stasisScale = 1.35f;
        [Tooltip("Curva de mezcla de escala (0=base, 1=stasis).")]
        public AnimationCurve scaleEase = new AnimationCurve(
            new Keyframe(0, 0, 0, 2f),   // arranque suave
            new Keyframe(0.8f, 1.05f),   // pequeño overshoot
            new Keyframe(1, 1)           // settle
        );

        [Header("Smoothing (Hz = respuesta por segundo)")]
        public float onLerpHz = 10f;
        public float offLerpHz = 6f;

        [Header("Hysteresis (anti-parpadeo)")]
        public float acquireHoldSeconds = 0.04f;
        public float loseHoldSeconds = 0.08f;

        [Header("Pulse (respiración)")]
        [Tooltip("Activar pulso respirante permanente.")]
        public bool enablePulse = true;
        [Tooltip("Amplitud base del pulso sobre la escala (porcentaje). 0.02 = +2%/-2%.")]
        public float basePulseAmplitude = 0.02f;
        [Tooltip("Frecuencia base (Hz) del pulso.")]
        public float basePulseHz = 2f;
        [Tooltip("Multiplicador de amplitud cuando hay target.")]
        public float focusPulseMultiplier = 2f;

        [Header("Overlay opcional (glow)")]
        [Tooltip("Segunda Image (más grande) que respira y gana alpha al enfocar.")]
        public Image overlayImage;
        [Tooltip("Escala del overlay respecto al crosshair base.")]
        public float overlayScaleMultiplier = 1.2f;
        [Tooltip("Alpha del overlay sin target.")]
        [Range(0f, 1f)] public float overlayBaseAlpha = 0.05f;
        [Tooltip("Alpha del overlay con target.")]
        [Range(0f, 1f)] public float overlayStasisAlpha = 0.25f;
        [Tooltip("Pulso adicional del overlay (suma a la escala).")]
        public float overlayPulseAmplitude = 0.015f;
        [Tooltip("Hz del overlay (puede ser != del pulso base).")]
        public float overlayPulseHz = 2.5f;

        [Header("Debug")]
        public bool drawRay = true;
        public Color rayColorHit = Color.green;
        public Color rayColorMiss = Color.red;

        // ---------------- Eventos ----------------
        public event Action<IStasis> OnAcquireTarget = delegate { };
        public event Action<IStasis> OnLoseTarget = delegate { };

        // Runtime
        private bool _wantTarget;
        private bool _hasTarget;
        private float _acquireTimer;
        private float _loseTimer;

        private float _mix;            // 0..1 mezcla visual (color/escala)
        private Color _currentColor;
        private float _currentScale;

        public IStasis currentStasis { get; private set; }

        // Cache
        private static readonly RaycastHit[] _hit = new RaycastHit[1];

        private void Reset()
        {
            cam = Camera.main;
        }

        private void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (crosshairImage != null && crosshairRect == null)
                crosshairRect = crosshairImage.rectTransform;

            _currentColor = baseColor;
            _currentScale = baseScale;
            ApplyVisualsImmediate();
        }

        private void Update()
        {
            // 1) Detección
            _wantTarget = TryDetectStasis(out var stasisFound);

            if (_wantTarget)
            {
                _loseTimer = 0f;
                currentStasis = stasisFound;

                _acquireTimer += Time.unscaledDeltaTime;
                if (!_hasTarget && _acquireTimer >= acquireHoldSeconds)
                {
                    _hasTarget = true;
                    OnAcquireTarget(currentStasis);
                }
            }
            else
            {
                _acquireTimer = 0f;
                if (_hasTarget)
                {
                    _loseTimer += Time.unscaledDeltaTime;
                    if (_loseTimer >= loseHoldSeconds)
                    {
                        _hasTarget = false;
                        _loseTimer = 0f;
                        var lost = currentStasis;
                        currentStasis = null;
                        OnLoseTarget(lost);
                    }
                }
            }

            // 2) Mezcla (0..1) con easing y suavizado exponencial
            float dt = Time.unscaledDeltaTime;
            float targetMix = _hasTarget ? 1f : 0f;
            float hz = _hasTarget ? onLerpHz : offLerpHz;
            float k = 1f - Mathf.Exp(-hz * dt);
            _mix = Mathf.LerpUnclamped(_mix, targetMix, k);

            // Curvas de easing
            float colorT = Mathf.Clamp01(colorEase.Evaluate(_mix));
            float scaleT = Mathf.Clamp01(scaleEase.Evaluate(_mix));

            // 3) Color + Escala base
            _currentColor = Color.LerpUnclamped(baseColor, stasisColor, colorT);
            _currentScale = Mathf.LerpUnclamped(baseScale, stasisScale, scaleT);

            // 4) Pulso respirante
            if (enablePulse && crosshairRect != null)
            {
                float amp = basePulseAmplitude * (_hasTarget ? focusPulseMultiplier : 1f);
                float pulse = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * basePulseHz) * amp;
                _currentScale += pulse;
            }

            // 5) Aplicar visuals (principal)
            ApplyMainVisuals();

            // 6) Overlay opcional (glow)
            if (overlayImage != null)
                ApplyOverlayVisuals();

            // 7) Debug
            if (drawRay && cam != null)
            {
                Vector3 o = cam.transform.position;
                Vector3 d = cam.transform.forward * maxDistance;
                Debug.DrawRay(o, d, _hasTarget ? rayColorHit : rayColorMiss, 0f, false);
            }
        }

        private void ApplyMainVisuals()
        {
            if (crosshairImage != null) crosshairImage.color = _currentColor;
            if (crosshairRect != null)
            {
                float s = _currentScale;
                crosshairRect.localScale = new Vector3(s, s, 1f);
            }
        }

        private void ApplyOverlayVisuals()
        {
            // Alpha mezcla
            float targetA = Mathf.Lerp(overlayBaseAlpha, overlayStasisAlpha, _mix);
            Color c = overlayImage.color;
            c.a = targetA;
            overlayImage.color = c;

            // Escala overlay = escala del main * multiplicador + pulso
            float baseS = _currentScale * overlayScaleMultiplier;
            float pulse = overlayPulseAmplitude * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * overlayPulseHz) * (_hasTarget ? 1.25f : 1f);
            float s = baseS + pulse;

            var rt = overlayImage.rectTransform;
            rt.localScale = new Vector3(s, s, 1f);
        }

        private void ApplyVisualsImmediate()
        {
            if (crosshairImage != null) crosshairImage.color = _currentColor;
            if (crosshairRect != null) crosshairRect.localScale = Vector3.one * _currentScale;

            if (overlayImage != null)
            {
                Color c = overlayImage.color; c.a = overlayBaseAlpha; overlayImage.color = c;
                float s = baseScale * overlayScaleMultiplier;
                overlayImage.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        private bool TryDetectStasis(out IStasis stasis)
        {
            stasis = null;
            if (cam == null) cam = Camera.main;
            if (cam == null) return false;

            int n = Physics.RaycastNonAlloc(
                cam.transform.position,
                cam.transform.forward,
                _hit,
                maxDistance,
                proxyLayerMask,
                triggerInteraction
            );
            if (n <= 0) return false;

            var h = _hit[0];

            // Lookup inmediato por collider del proxy
            if (StasisRegistry.TryGet(h.collider, out stasis))
                return true;

            // Fallbacks de seguridad
            var proxy = h.collider.GetComponent<StasisProxy>();
            if (proxy != null && proxy.owner is IStasis sOwner)
            {
                stasis = sOwner;
                return true;
            }

            var mono = h.collider.GetComponentInParent<MonoBehaviour>();
            if (mono is IStasis s) { stasis = s; return true; }

            return false;
        }
    }
}
