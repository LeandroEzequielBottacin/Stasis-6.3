using Player.Scripts.MovementFSM.MVC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace _Ian.VFX.Smoke
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public class ColdHazardVolumeLink : MonoBehaviour
    {
        public float value;
        public float potencia;
        public float weight;
        public float vign;
        [Header("Referencia al Player")]
        [Tooltip("Arrastrá el Model del jugador aquí.")]
        public Model playerModel;

        private Volume _volume;
        private VolumeProfile _profile;

        // Cached override references
        private Vignette _vig;
        private WhiteBalance _wb;
        private ColorAdjustments _colorAdj;
        private ChromaticAberration _chrom;

        [Header("Curva General")]
        [Tooltip("X: peligro (0 normal, 1 casi muerto) | Y: intensidad visual (0–1).")]
        public AnimationCurve dangerToEffect = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Hace el efecto MUCHO más fuerte cerca del final. (1 = lineal, 2 = agresivo)")]
        [Range(1f, 5f)] public float dangerExponent = 2.2f;

        [Header("Volume Weight")]
        [Range(0f, 1f)] public float maxWeight = 1f;

        [Header("Vignette")]
        public bool driveVignette = true;
        [Range(0f, 1f)] public float minVignette = 0f;
        [Range(0f, 1f)] public float maxVignette = 0.7f;
        public Color vignetteColor = new Color(0.55f, 0.75f, 1f, 1f); // azul frío


        [Header("White Balance")]
        public bool driveWhiteBalance = true;
        public float minTemperature = -80f;
        public float maxTemperature = 0f;


        [Header("Color Adjustments")]
        public bool driveColorAdjustments = true;
        public float minSaturation = -40f;
        public float maxSaturation = 0f;


        [Header("Chromatic Aberration")]
        public bool driveChromaticAberration = true;
        [Range(0f, 1f)] public float minChromatic = 0f;
        [Range(0f, 1f)] public float maxChromatic = 0.4f;


        void Awake()
        {
            _volume = GetComponent<Volume>();
            _profile = _volume.profile;

            if (!_profile)
            {
                Debug.LogError($"[ColdHazardVolumeLink] El Volume no tiene Profile. Creá uno y asignalo.");
                enabled = false;
                return;
            }

            // Cache de overrides
            _profile.TryGet(out _vig);
            _profile.TryGet(out _wb);
            _profile.TryGet(out _colorAdj);
            _profile.TryGet(out _chrom);

            if (!playerModel)
            {
                Debug.LogError($"[ColdHazardVolumeLink] Falta asignar el Model del Player.");
            }
        }

        void LateUpdate()
        {
            if (!playerModel) return;

            // m = 1 → normal, m = 0 → casi muerto
            float m = Mathf.Clamp01(playerModel.hazardSpeedMultiplier);

            // "danger" = cuánto riesgo tenemos, invertimos m
            float danger = 1 - m;
            value = danger;

            // Pasamos por la curva
            float k = dangerToEffect.Evaluate(danger);

            // Exponente para hacerlo más agresivo cerca del final
            if (dangerExponent > 0f)
                k = Mathf.Pow(k, dangerExponent);
            potencia = k;
            k = Mathf.Clamp01(k);

            // Peso del volume
            _volume.weight = maxWeight * k;
            weight = _volume.weight;

            // ---- OVERDRIVES ----

            if (driveVignette && _vig != null)
            {
                _vig.intensity.overrideState = true;
                _vig.intensity.value = Mathf.Lerp(minVignette, maxVignette, k);
                vign = _vig.intensity.value;
                _vig.color.overrideState = true;
                _vig.color.value = vignetteColor;
            }

            if (driveWhiteBalance && _wb != null)
            {
                _wb.temperature.overrideState = true;
                _wb.temperature.value = Mathf.Lerp(minTemperature, maxTemperature, k);
            }

            if (driveColorAdjustments && _colorAdj != null)
            {
                _colorAdj.saturation.overrideState = true;
                _colorAdj.saturation.value = Mathf.Lerp(minSaturation, maxSaturation, k);
            }

            if (driveChromaticAberration && _chrom != null)
            {
                _chrom.intensity.overrideState = true;
                _chrom.intensity.value = Mathf.Lerp(minChromatic, maxChromatic, k);
            }
        }
    }
}
