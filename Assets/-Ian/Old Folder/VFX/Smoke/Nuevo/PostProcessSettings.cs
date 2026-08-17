// =========================================================
// POST PROCESS
// =========================================================

using _Ian.VFX.Smoke;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
public class PostProcessSettings
{
    [Header("Volume")]
    public Volume volume;

    [Header("Curva General")]
    [Tooltip("X = peligro. Y = intensidad visual.")]
    public AnimationCurve dangerToEffect =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Hace que el efecto crezca con más agresividad.")]
    [Range(1f, 5f)]
    public float dangerExponent = 2.2f;

    [Header("Volume Weight")]
    [Range(0f, 1f)]
    public float maxWeight = 1f;

    // =====================================================
    // VIGNETTE
    // =====================================================

    [Header("Vignette")]
    public bool driveVignette = true;

    [Range(0f, 1f)]
    public float minVignette = 0f;

    [Range(0f, 1f)]
    public float maxVignette = 0.7f;

    public Color vignetteColor =
        new Color(0.55f, 0.75f, 1f, 1f);

    // =====================================================
    // WHITE BALANCE
    // =====================================================

    [Header("White Balance")]
    public bool driveWhiteBalance = true;

    public float minTemperature = -80f;
    public float maxTemperature = 0f;

    // =====================================================
    // COLOR ADJUSTMENTS
    // =====================================================

    [Header("Color Adjustments")]
    public bool driveColorAdjustments = true;

    public float minSaturation = -40f;
    public float maxSaturation = 0f;

    // =====================================================
    // CHROMATIC ABERRATION
    // =====================================================

    [Header("Chromatic Aberration")]
    public bool driveChromaticAberration = true;

    [Range(0f, 1f)]
    public float minChromatic = 0f;

    [Range(0f, 1f)]
    public float maxChromatic = 0.4f;

    // =====================================================
    // DEBUG
    // =====================================================

    [Header("Runtime Debug")]
    public float dangerValue;
    public float effectPower;
    public float currentWeight;
    public float currentVignette;

    [NonSerialized]
    private VolumeProfile _profile;

    [NonSerialized]
    private Vignette _vignette;

    [NonSerialized]
    private WhiteBalance _whiteBalance;

    [NonSerialized]
    private ColorAdjustments _colorAdjustments;

    [NonSerialized]
    private ChromaticAberration _chromatic;

    public void Initialize(HazardController controller)
    {
        if (volume == null)
            return;

        _profile = volume.profile;

        if (_profile == null)
        {
            Debug.LogError(
                $"[{controller.name}] El Volume asignado no tiene Profile."
            );

            return;
        }

        _profile.TryGet(out _vignette);
        _profile.TryGet(out _whiteBalance);
        _profile.TryGet(out _colorAdjustments);
        _profile.TryGet(out _chromatic);

        volume.weight = 0f;
    }

    public void ApplyDanger(float danger)
    {
        if (volume == null || _profile == null)
            return;

        danger = Mathf.Clamp01(danger);
        dangerValue = danger;

        float k = dangerToEffect.Evaluate(danger);

        if (dangerExponent > 0f)
            k = Mathf.Pow(k, dangerExponent);

        effectPower = k;

        k = Mathf.Clamp01(k);

        volume.weight = maxWeight * k;
        currentWeight = volume.weight;

        if (driveVignette && _vignette != null)
        {
            _vignette.intensity.overrideState = true;

            _vignette.intensity.value = Mathf.Lerp(
                minVignette,
                maxVignette,
                k
            );

            currentVignette = _vignette.intensity.value;

            _vignette.color.overrideState = true;
            _vignette.color.value = vignetteColor;
        }

        if (driveWhiteBalance && _whiteBalance != null)
        {
            _whiteBalance.temperature.overrideState = true;

            _whiteBalance.temperature.value = Mathf.Lerp(
                minTemperature,
                maxTemperature,
                k
            );
        }

        if (driveColorAdjustments && _colorAdjustments != null)
        {
            _colorAdjustments.saturation.overrideState = true;

            _colorAdjustments.saturation.value = Mathf.Lerp(
                minSaturation,
                maxSaturation,
                k
            );
        }

        if (driveChromaticAberration && _chromatic != null)
        {
            _chromatic.intensity.overrideState = true;

            _chromatic.intensity.value = Mathf.Lerp(
                minChromatic,
                maxChromatic,
                k
            );
        }
    }

    public void ResetEffect()
    {
        dangerValue = 0f;
        effectPower = 0f;
        currentWeight = 0f;
        currentVignette = 0f;

        if (volume != null)
            volume.weight = 0f;
    }
}
    