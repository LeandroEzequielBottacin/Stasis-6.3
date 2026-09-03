using UnityEngine;

public partial class ProceduralLightning
{
    [Tooltip("Buffer de direcciones radiales de los puntos del arco de carga. Tambien indica que el visual de carga fue preparado.")]
    private Vector3[] chargeDirections;

    [Tooltip("Renderers propios del aura, creados una sola vez y reutilizados. No dependen de Branch Renderers del prefab.")]
    private LightningBranchRenderer[] auraBranchRenderers;

    public void PrepareAuraBranches(int count)
    {
        count = Mathf.Clamp(count, 0, 16);

        if (auraBranchRenderers != null && auraBranchRenderers.Length == count)
            return;

        if (auraBranchRenderers != null)
        {
            foreach (LightningBranchRenderer branch in auraBranchRenderers)
            {
                if (branch.glowRenderer)
                    Destroy(branch.glowRenderer.gameObject);

                if (branch.coreRenderer)
                    Destroy(branch.coreRenderer.gameObject);
            }
        }

        auraBranchRenderers = new LightningBranchRenderer[count];

        for (int i = 0; i < count; i++)
        {
            LightningBranchRenderer branch = new LightningBranchRenderer();
            branch.glowRenderer = CreateAuraBranchRenderer(mainGlowRenderer, "Aura Branch Glow " + i, mainGlowWidth);
            branch.coreRenderer = CreateAuraBranchRenderer(mainCoreRenderer, "Aura Branch Core " + i, mainCoreWidth);
            branch.positions = new Vector3[Mathf.Max(2, branchSegmentCount) + 1];
            auraBranchRenderers[i] = branch;
        }
    }

    private LineRenderer CreateAuraBranchRenderer(LineRenderer template, string objectName, float width)
    {
        if (!template)
            return null;

        GameObject branchObject = new GameObject(objectName);
        branchObject.layer = template.gameObject.layer;
        branchObject.transform.SetParent(transform, false);

        LineRenderer renderer = branchObject.AddComponent<LineRenderer>();
        renderer.sharedMaterials = template.sharedMaterials;
        renderer.colorGradient = template.colorGradient;
        renderer.sortingLayerID = template.sortingLayerID;
        renderer.sortingOrder = template.sortingOrder;
        renderer.renderingLayerMask = template.renderingLayerMask;
        renderer.generateLightingData = template.generateLightingData;

        template.GetPropertyBlock(materialProperties);
        renderer.SetPropertyBlock(materialProperties);

        ConfigureLineRenderer(renderer, width, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        renderer.positionCount = Mathf.Max(2, branchSegmentCount) + 1;
        renderer.enabled = false;
        return renderer;
    }

    private void HideAuraBranches()
    {
        if (auraBranchRenderers == null)
            return;

        foreach (LightningBranchRenderer branch in auraBranchRenderers)
        {
            branch.isVisible = false;
            SetBranchRendererEnabled(branch, false);
        }
    }

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
        HideAuraBranches();
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

    public void DrawChargeAura(Vector3 center, float radius, Vector3 startDirection, Vector3 tangent, float arcRadians, float surfaceOffset, float intensity, float widthMultiplier, int seed, int maximumBranches, Vector2 branchLengths, float branchChance, float roughness, float spread, float branchWidthMultiplier)
    {
        DrawChargeSphere(center, radius, startDirection, tangent, arcRadians, surfaceOffset, intensity, widthMultiplier, seed);

        if (chargeDirections == null || mainPositions == null || mainPositions.Length < 3)
        {
            HideAuraBranches();
            return;
        }

        if (auraBranchRenderers == null)
            return;

        int limit = Mathf.Clamp(maximumBranches, 0, auraBranchRenderers.Length);
        float minimumLength = Mathf.Max(0.01f, branchLengths.x);
        float maximumLength = Mathf.Max(minimumLength, branchLengths.y);
        float chance = Mathf.Clamp01(branchChance);
        float branchIntensity = Mathf.Max(0f, intensity);
        float branchWidth = Mathf.Max(0f, widthMultiplier) * Mathf.Max(0f, branchWidthMultiplier);
        int segments = Mathf.Max(2, branchSegmentCount);

        for (int branchIndex = 0; branchIndex < auraBranchRenderers.Length; branchIndex++)
        {
            LightningBranchRenderer branch = auraBranchRenderers[branchIndex];

            if (branch == null)
                continue;

            int sampleBase = branchIndex * 101;
            branch.isVisible = branchIndex < limit && AuraBranchRandom01(seed, sampleBase) < chance;
            SetBranchRendererEnabled(branch, branch.isVisible);

            if (!branch.isVisible)
                continue;

            if (branch.positions == null || branch.positions.Length != segments + 1)
            {
                branch.positions = new Vector3[segments + 1];
            }

            // Nace exactamente en un punto del arco que abraza la esfera.
            float startRatio = Mathf.Lerp(0.1f, 0.9f, AuraBranchRandom01(seed, sampleBase + 1));
            int startIndex = Mathf.Clamp(Mathf.RoundToInt(startRatio * (mainPositions.Length - 1)), 1, mainPositions.Length - 2);
            Vector3 start = mainPositions[startIndex];
            Vector3 outward = (start - center).normalized;
            CreatePerpendicularAxes(outward, out Vector3 side, out Vector3 up);

            float length = Mathf.Lerp(minimumLength, maximumLength, AuraBranchRandom01(seed, sampleBase + 2));
            float sideways = AuraBranchRandom01(seed, sampleBase + 3) * 2f - 1f;
            float vertical = AuraBranchRandom01(seed, sampleBase + 4) * 2f - 1f;
            Vector3 direction = (outward + (side * sideways + up * vertical) * Mathf.Max(0f, spread)).normalized;
            float amplitude = Mathf.Max(0f, roughness);

            for (int pointIndex = 0; pointIndex <= segments; pointIndex++)
            {
                float progress = pointIndex / (float)segments;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float firstNoise = AuraBranchRandom01(seed, sampleBase + 10 + pointIndex * 2) * 2f - 1f;
                float secondNoise = AuraBranchRandom01(seed, sampleBase + 11 + pointIndex * 2) * 2f - 1f;
                Vector3 jitter = (side * firstNoise + up * secondNoise) * amplitude * envelope;

                // El ruido es tangente. La componente radial siempre avanza hacia afuera.
                branch.positions[pointIndex] = start + direction * length * progress + jitter;
            }

            branch.positions[0] = start;
            branch.positions[segments] = start + direction * length;

            if (branch.glowRenderer)
            {
                branch.glowRenderer.positionCount = branch.positions.Length;
                branch.glowRenderer.widthMultiplier = mainGlowWidth * branchWidth;
                SetRendererIntensity(branch.glowRenderer, mainGlowIntensity * branchIntensity);
            }

            if (branch.coreRenderer)
            {
                branch.coreRenderer.positionCount = branch.positions.Length;
                branch.coreRenderer.widthMultiplier = mainCoreWidth * branchWidth;
                SetRendererIntensity(branch.coreRenderer, mainCoreIntensity * branchIntensity);
            }

            ApplyBranchPositions(branch);
        }
    }

    private static float AuraBranchRandom01(int seed, int sample)
    {
        unchecked
        {
            uint value = (uint)seed ^ ((uint)sample * 0x9E3779B9u);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216f;
        }
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