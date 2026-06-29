using System.Collections.Generic;
using Player.Stasis;
using UnityEngine;

namespace UIScripts.FeedBack_UI.Crosshair
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Assets/Scripts/UIScripts/FeedBack_UI/Crosshair")]
    public class StasisHighlighter : MonoBehaviour
    {
        [Header("Owner (IStasis)")]
        [Tooltip("Si se deja vacío, busca IStasis en este GO o sus padres.")]
        public MonoBehaviour stasisOwner;
        private IStasis _stasis;

        [Header("Renderers a resaltar")]
        [Tooltip("Si está vacío, se buscan todos los Renderer en hijos.")]
        public List<Renderer> renderers = new List<Renderer>();

        [Header("Emission")]
        public bool useEmission = true;
        public Color highlightColor = Color.cyan;
        [Min(0f)] public float highlightIntensity = 1.2f;
        [Tooltip("Propiedad HDR del shader (URP Lit usa _EmissionColor).")]
        public string emissionProp = "_EmissionColor";
        [Tooltip("Habilitar el keyword _EMISSION una vez (utiliza renderer.material).")]
        public bool enableEmissionKeywordOnce;
        public string emissionKeyword = "_EMISSION";

        [Header("Outline")]
        public bool useOutline = true;
        [Tooltip("Propiedad de grosor de borde (tu shader usa _BorderThickness).")]
        public string outlineProp = "_BorderThickness";
        public float outlineOn = 1.05f;
        public float outlineOff;

        [Header("Animación")]
        [Tooltip("Tiempo de fundido al entrar/salir (seg).")]
        public float fadeSeconds = 0.12f;
        [Tooltip("Velocidad del pulso (Hz = latidos/seg). 0 = sin pulso")]
        public float pulseHz = 1.7f;
        [Range(0f, 1f)] public float pulseDepth = 0.25f;

        [Header("Crosshair source (auto-resolve si está vacío)")]
        public CrosshairStasisController crosshair;

        // Runtime
        private MaterialPropertyBlock _mpb;
        private float _t;              // 0..1 (fade)
        private bool _focused;         // estado de foco (entre acquire/lose)
        private bool _subscribed;      // para evitar dobles suscripciones
        private Camera _camCache;

        private void Awake()
        {
            // Resolver IStasis
            if (stasisOwner == null)
                stasisOwner = GetComponentInParent<MonoBehaviour>();

            _stasis = stasisOwner as IStasis;
            if (_stasis == null)
            {
                var monos = GetComponentsInParent<MonoBehaviour>(true);
                for (int i = 0; i < monos.Length; i++)
                {
                    if (monos[i] is IStasis s) { _stasis = s; break; }
                }
            }

            if (_stasis == null)
                Debug.LogWarning($"[StasisHighlighter] No se encontró IStasis en '{name}'.", this);

            // Renderers
            if (renderers == null || renderers.Count == 0)
                GetComponentsInChildren(true, renderers);

            _mpb = new MaterialPropertyBlock();

            // (Opcional) habilitar keyword de emisión una vez por renderer
            if (enableEmissionKeywordOnce && useEmission)
            {
                for (int i = 0; i < renderers.Count; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    var mat = r.material; // instancia por-renderer (costo único)
                    if (mat != null && !mat.IsKeywordEnabled(emissionKeyword))
                        mat.EnableKeyword(emissionKeyword);
                }
            }
        }

        private void OnEnable()
        {
            // Resolver crosshair si no está asignado
            if (crosshair == null)
            {
                if (_camCache == null) _camCache = Camera.main;
                if (_camCache != null) crosshair = _camCache.GetComponent<CrosshairStasisController>();
            }

            if (crosshair != null && !_subscribed)
            {
                crosshair.OnAcquireTarget += HandleAcquire;
                crosshair.OnLoseTarget += HandleLose;
                _subscribed = true;
            }

            // Estado inicial apagado
            _t = 0f;
            ApplyVisuals(0f, 0f);
        }

        private void OnDisable()
        {
            if (crosshair != null && _subscribed)
            {
                crosshair.OnAcquireTarget -= HandleAcquire;
                crosshair.OnLoseTarget -= HandleLose;
                _subscribed = false;
            }
        }

        private void HandleAcquire(IStasis target)
        {
            if (target == _stasis) _focused = true;
        }

        private void HandleLose(IStasis target)
        {
            if (target == _stasis) _focused = false;
        }

        private void Update()
        {
            // Fade
            float speed = (fadeSeconds > 0.0001f) ? (1f / fadeSeconds) : 999f;
            float dir = _focused ? 1f : -1f;
            _t = Mathf.Clamp01(_t + dir * speed * Time.unscaledDeltaTime);

            // Pulso (solo cuando hay foco)
            float pulse = 0f;
            if (_t > 0f && pulseHz > 0f && pulseDepth > 0f)
            {
                pulse = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * pulseHz) * pulseDepth; // -d..+d
            }

            ApplyVisuals(_t, pulse);
        }

        private void ApplyVisuals(float t, float pulse)
        {
            // Emission HDR: color * intensidad * (1+pulse) * t
            Color emissiveHDR = useEmission
                ? highlightColor * (highlightIntensity * Mathf.Max(0f, (1f + pulse))) * t
                : Color.black;

            // Outline: LERP con leve influencia del pulso
            float outline = useOutline
                ? Mathf.Lerp(outlineOff, outlineOn, Mathf.Clamp01(t * (1f + pulse)))
                : outlineOff;

            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);

                if (useEmission)
                    _mpb.SetColor(emissionProp, emissiveHDR);

                if (useOutline)
                    _mpb.SetFloat(outlineProp, outline);

                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
