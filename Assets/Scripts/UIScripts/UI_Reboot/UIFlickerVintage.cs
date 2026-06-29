using UnityEngine;
using UnityEngine.UI;

namespace UIScripts.UI_Reboot
{
    [DisallowMultipleComponent]
    public class UIFlickerVintage : MonoBehaviour
    {
        [Header("Target (auto si quedan vacíos)")]
        public CanvasGroup canvasGroup;   // Si está, se usa alpha global
        public Graphic graphic;           // Image o TMPUGUI (usa color.a)

        [Header("Flicker base")]
        [Range(0f,1f)] public float baseAlpha = 0.20f;
        [Range(0f,1f)] public float amplitude = 0.08f;      // Profundidad del flicker
        public float noiseFrequency = 12f;                   // Hz del ruido Perlin
        public float sineFrequency = 60f;                    // Hz del “hum” de tubo
        [Range(0f,1f)] public float sineWeight = 0.25f;      // Mezcla ruido/seno
        public int noiseSeed = 12345;
        public bool unscaledTime = true;                     // Ignorar timeScale

        [Header("Glitch bursts")]
        public bool bursts = true;
        public Vector2 burstInterval = new Vector2(2f, 6f);  // cada X–Y seg
        public float burstDuration = 0.12f;                  // seg
        public float burstMultiplier = 3f;                   // cuán fuerte el burst

        [Header("Horizontal Jitter (opcional)")]
        public RectTransform jitterTarget;                   // si null, usa el propio
        public float jitterPixels = 1.0f;                    // 0 = off

        // internos
        Color _origColor;
        RectTransform _rt;
        float _burstEnd;
        float _nextBurst;

        void Reset() { TryAuto(); }
        void Awake() { TryAuto(); }
        void OnEnable() { ScheduleNextBurst(); }

        void TryAuto()
        {
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            if (!graphic) graphic = GetComponent<Graphic>();
            if (!_rt) _rt = jitterTarget ? jitterTarget : GetComponent<RectTransform>();
            if (graphic) _origColor = graphic.color;
        }

        void ScheduleNextBurst()
        {
            if (!bursts) { _nextBurst = float.PositiveInfinity; return; }
            float now = Time.time;
            _nextBurst = now + Random.Range(burstInterval.x, burstInterval.y);
        }

        void Update()
        {
            float t = unscaledTime ? Time.unscaledTime : Time.time;

            // disparar burst
            if (bursts && t >= _nextBurst)
            {
                _burstEnd = t + burstDuration;
                ScheduleNextBurst();
            }

            float amp = amplitude;
            if (t < _burstEnd) amp *= burstMultiplier;

            // mezcla ruido + seno
            float n = Mathf.PerlinNoise(t * noiseFrequency, noiseSeed * 0.001f) * 2f - 1f;
            float s = Mathf.Sin(t * Mathf.PI * 2f * sineFrequency);
            float mix = Mathf.Lerp(n, s, sineWeight);

            float a = Mathf.Clamp01(baseAlpha + amp * mix);

            // aplicar alpha
            if (canvasGroup)
            {
                canvasGroup.alpha = a;
            }
            else if (graphic)
            {
                var c = _origColor;
                c.a = a;
                graphic.color = c;
            }

            // jitter horizontal opcional
            if (_rt && jitterPixels > 0f)
            {
                float j = (Mathf.PerlinNoise(t * 60f, 999f) * 2f - 1f) * jitterPixels;
                var pos = _rt.anchoredPosition;
                pos.x = Mathf.Round(j);
                _rt.anchoredPosition = pos;
            }
        }
    }
}
