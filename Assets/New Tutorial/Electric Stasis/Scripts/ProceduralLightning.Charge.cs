using UnityEngine;

public partial class ProceduralLightning
{
    [Tooltip("Buffer de direcciones radiales de los puntos del arco de carga. Tambien indica que el visual de carga fue preparado.")]
    private Vector3[] chargeDirections;

    public void PrepareChargeVisual()
    {
        enabled = false;
        playOnAwake = false;

        if (materialProperties == null)
            materialProperties = new MaterialPropertyBlock();

        Stop();

        if (audioSource)
            audioSource.Stop();

        if (impactFlashParticles)
            impactFlashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (impactSparkParticles)
            impactSparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ConfigureAllLineRenderers();
        RebuildPositionArrays();

        chargeDirections = new Vector3[mainPositions.Length];

        SetAllBranchRenderersEnabled(false);
    }

    public void HideChargeVisual()
    {
        SetBoltRenderersEnabled(false);

        if (sceneLight)
            sceneLight.enabled = false;
    }

    public void DrawChargeInward(Vector3 tail, Vector3 head, float intensity, float widthMultiplier, float roughness, int seed)
    {
        if (chargeDirections == null)
            return;

        Vector3 delta = head - tail;

        if (delta.sqrMagnitude < 0.000001f)
        {
            HideChargeVisual();
            return;
        }

        CreatePerpendicularAxes(delta.normalized, out Vector3 side, out Vector3 up);

        int last = mainPositions.Length - 1;

        for (int i = 0; i <= last; i++)
        {
            float t = i / (float)last;
            float a = ChargeNoise(seed, i, 0);
            float b = ChargeNoise(seed, i, 91);
            float envelope = Mathf.Sin(t * Mathf.PI);

            Vector3 offset = (side * a + up * b) * roughness * envelope;
            mainPositions[i] = Vector3.Lerp(tail, head, t) + offset;
        }

        ShowChargeGeometry(intensity, widthMultiplier);
    }

    public void DrawChargeSphere(Vector3 center, float radius, Vector3 startDirection, Vector3 tangent, float arcRadians, float surfaceOffset, float intensity, float widthMultiplier, int seed)
    {
        if (chargeDirections == null)
            return;

        Vector3 normal = Vector3.Cross(startDirection, tangent).normalized;
        int last = mainPositions.Length - 1;

        for (int i = 0; i <= last; i++)
        {
            float t = i / (float)last;
            float angle = arcRadians * t;
            float noise = ChargeNoise(seed, i, 32) * 0.07f * Mathf.Sin(t * Mathf.PI);

            Vector3 direction = startDirection * Mathf.Cos(angle) + tangent * Mathf.Sin(angle);
            chargeDirections[i] = (direction + normal * noise).normalized;
        }

        for (int i = 0; i <= last; i++)
        {
            float dot = 1f;

            if (i > 0)
                dot = Mathf.Min(dot, Vector3.Dot(chargeDirections[i], chargeDirections[i - 1]));

            if (i < last)
                dot = Mathf.Min(dot, Vector3.Dot(chargeDirections[i], chargeDirections[i + 1]));

            float cosHalfAngle = Mathf.Sqrt(Mathf.Max(0.01f, (1f + dot) * 0.5f));
            float safeRadius = (radius + Mathf.Max(0.001f, surfaceOffset)) / cosHalfAngle;

            mainPositions[i] = center + chargeDirections[i] * safeRadius;
        }

        ShowChargeGeometry(intensity, widthMultiplier);
    }

    private static float ChargeNoise(int seed, int sample, int offset)
    {
        return Mathf.PerlinNoise(sample * 3.17f + offset, (seed % 10000) * 0.137f) * 2f - 1f;
    }

    private void ShowChargeGeometry(float intensity, float widthMultiplier)
    {
        if (mainGlowRenderer)
        {
            mainGlowRenderer.widthMultiplier = mainGlowWidth * widthMultiplier;
            mainGlowRenderer.SetPositions(mainPositions);

            SetRendererIntensity(mainGlowRenderer, mainGlowIntensity * intensity);

            mainGlowRenderer.enabled = true;
        }

        if (mainCoreRenderer)
        {
            mainCoreRenderer.widthMultiplier = mainCoreWidth * widthMultiplier;
            mainCoreRenderer.SetPositions(mainPositions);

            SetRendererIntensity(mainCoreRenderer, mainCoreIntensity * intensity);

            mainCoreRenderer.enabled = true;
        }
    }
}