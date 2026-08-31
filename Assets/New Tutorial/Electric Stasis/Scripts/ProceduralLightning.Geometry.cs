using UnityEngine;
using UnityEngine.Rendering;

public partial class ProceduralLightning
{
    private void ConfigureAllLineRenderers()
    {
        ConfigureLineRenderer(mainGlowRenderer, mainGlowWidth, mainWidthCurve);
        ConfigureLineRenderer(mainCoreRenderer, mainCoreWidth, mainWidthCurve);

        if (branchRenderers == null)
            return;

        foreach (LightningBranchRenderer branch in branchRenderers)
        {
            if (branch == null)
                continue;

            ConfigureLineRenderer(branch.glowRenderer, branchGlowWidth, branchWidthCurve);
            ConfigureLineRenderer(branch.coreRenderer, branchCoreWidth, branchWidthCurve);
        }
    }

    private static void ConfigureLineRenderer(LineRenderer lineRenderer, float width, AnimationCurve widthCurve)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.widthMultiplier = width;
        lineRenderer.widthCurve = widthCurve;
        lineRenderer.numCapVertices = 2;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
    }

    private void RebuildPositionArrays()
    {
        mainSegmentCount = Mathf.Max(2, mainSegmentCount);
        mainPositions = new Vector3[mainSegmentCount + 1];

        if (mainGlowRenderer != null)
            mainGlowRenderer.positionCount = mainPositions.Length;

        if (mainCoreRenderer != null)
            mainCoreRenderer.positionCount = mainPositions.Length;

        branchSegmentCount = Mathf.Max(2, branchSegmentCount);

        if (branchRenderers == null)
            return;

        foreach (LightningBranchRenderer branch in branchRenderers)
        {
            if (branch == null)
                continue;

            RebuildBranchPositions(branch);
        }
    }

    private void RebuildBranchPositions(LightningBranchRenderer branch)
    {
        branch.positions = new Vector3[branchSegmentCount + 1];

        if (branch.glowRenderer != null)
            branch.glowRenderer.positionCount = branch.positions.Length;

        if (branch.coreRenderer != null)
            branch.coreRenderer.positionCount = branch.positions.Length;
    }

    private void GenerateBoltGeometry()
    {
        if (sourceTransform == null)
            return;

        if (!useExternalEndPoint && targetTransform == null)
            return;

        if (mainPositions == null || mainPositions.Length != mainSegmentCount + 1)
            RebuildPositionArrays();

        Vector3 startPosition = sourceTransform.position;
        Vector3 desiredEndPosition = useExternalEndPoint
            ? currentEndPoint
            : targetTransform.position;
        ResolveTargetSurface(startPosition, desiredEndPosition, out Vector3 endPosition, out Vector3 impactNormal);

        currentImpactPosition = endPosition;
        currentImpactNormal = impactNormal;
        UpdateImpactTransform();

        Vector3 boltDirection = endPosition - startPosition;
        float boltDistance = boltDirection.magnitude;

        if (sceneLight != null && positionLightAtMidpoint)
            sceneLight.transform.position = Vector3.Lerp(startPosition, endPosition, 0.5f);

        if (boltDistance <= Mathf.Epsilon)
        {
            for (int index = 0; index < mainPositions.Length; index++)
                mainPositions[index] = startPosition;

            ApplyMainBoltPositions();
            SetAllBranchRenderersEnabled(false);
            return;
        }

        boltDirection /= boltDistance;
        CreatePerpendicularAxes(boltDirection, out Vector3 perpendicular, out Vector3 binormal);

        mainPositions[0] = startPosition;
        mainPositions[mainPositions.Length - 1] = endPosition;

        for (int index = 1; index < mainPositions.Length - 1; index++)
        {
            float progress = index / (float)mainSegmentCount;
            Vector3 centerPosition = Vector3.Lerp(startPosition, endPosition, progress);
            float endpointEnvelope = Mathf.Sin(progress * Mathf.PI);
            float randomPerpendicular = Random.Range(-1f, 1f);
            float randomBinormal = Random.Range(-1f, 1f);
            float spatialNoisePosition = progress * noiseScale;
            float timeNoisePosition = Time.time * noiseSpeed;
            float perpendicularNoise = Mathf.PerlinNoise(spatialNoisePosition + noiseSeed, timeNoisePosition) * 2f - 1f;
            float binormalNoise = Mathf.PerlinNoise(spatialNoisePosition + noiseSeed + 1000f, timeNoisePosition) * 2f - 1f;
            float perpendicularOffset = (randomPerpendicular * 0.85f + perpendicularNoise * 0.15f) * mainDisplacement;
            float binormalOffset = (randomBinormal * 0.85f + binormalNoise * 0.15f) * mainDisplacement;
            Vector3 offset = perpendicular * perpendicularOffset + binormal * binormalOffset;

            mainPositions[index] = centerPosition + offset * endpointEnvelope;
        }

        ApplyMainBoltPositions();
        GenerateBranchGeometries(boltDirection, boltDistance, perpendicular, binormal);
    }

    private void GenerateBranchGeometries(Vector3 mainDirection, float mainDistance, Vector3 perpendicular, Vector3 binormal)
    {
        if (branchRenderers == null)
            return;

        foreach (LightningBranchRenderer branch in branchRenderers)
        {
            if (branch == null)
                continue;

            branch.isVisible = Random.value <= branchVisibleChance;
            SetBranchRendererEnabled(branch, isMainBoltVisible && branch.isVisible);

            if (branch.isVisible)
                GenerateSingleBranchGeometry(branch, mainDirection, mainDistance, perpendicular, binormal);
        }
    }

    private void GenerateSingleBranchGeometry(LightningBranchRenderer branch, Vector3 mainDirection, float mainDistance, Vector3 perpendicular, Vector3 binormal)
    {
        if (branch == null)
            return;

        if (branch.positions == null || branch.positions.Length != branchSegmentCount + 1)
            RebuildBranchPositions(branch);

        int lastMainPositionIndex = mainPositions.Length - 1;
        int minimumStartIndex = Mathf.RoundToInt(lastMainPositionIndex * minimumBranchStart);
        int maximumStartIndex = Mathf.RoundToInt(lastMainPositionIndex * maximumBranchStart);

        minimumStartIndex = Mathf.Clamp(minimumStartIndex, 1, lastMainPositionIndex - 1);
        maximumStartIndex = Mathf.Clamp(maximumStartIndex, minimumStartIndex, lastMainPositionIndex - 1);

        int startIndex = Random.Range(minimumStartIndex, maximumStartIndex + 1);
        Vector3 branchStartPosition = mainPositions[startIndex];
        float branchLengthRatio = Random.Range(minimumBranchLengthRatio, maximumBranchLengthRatio);
        float branchLength = mainDistance * branchLengthRatio;
        float perpendicularDirection = Random.Range(-1f, 1f);
        float binormalDirection = Random.Range(-1f, 1f);
        Vector3 branchDirection = (mainDirection * branchForwardInfluence + perpendicular * perpendicularDirection + binormal * binormalDirection).normalized;
        Vector3 branchEndPosition = branchStartPosition + branchDirection * branchLength;

        CreatePerpendicularAxes(branchDirection, out Vector3 branchPerpendicular, out Vector3 branchBinormal);

        branch.positions[0] = branchStartPosition;
        branch.positions[branch.positions.Length - 1] = branchEndPosition;

        for (int index = 1; index < branch.positions.Length - 1; index++)
        {
            float progress = index / (float)branchSegmentCount;
            Vector3 centerPosition = Vector3.Lerp(branchStartPosition, branchEndPosition, progress);
            float endpointEnvelope = Mathf.Sin(progress * Mathf.PI);
            float perpendicularOffset = Random.Range(-branchDisplacement, branchDisplacement);
            float binormalOffset = Random.Range(-branchDisplacement, branchDisplacement);
            Vector3 offset = branchPerpendicular * perpendicularOffset + branchBinormal * binormalOffset;

            branch.positions[index] = centerPosition + offset * endpointEnvelope;
        }

        ApplyBranchPositions(branch);
    }

    private void ApplyMainBoltPositions()
    {
        if (mainGlowRenderer != null)
            mainGlowRenderer.SetPositions(mainPositions);

        if (mainCoreRenderer != null)
            mainCoreRenderer.SetPositions(mainPositions);
    }

    private static void ApplyBranchPositions(LightningBranchRenderer branch)
    {
        if (branch.glowRenderer != null)
            branch.glowRenderer.SetPositions(branch.positions);

        if (branch.coreRenderer != null)
            branch.coreRenderer.SetPositions(branch.positions);
    }

    private void ApplyVisualIntensity(float multiplier)
    {
        SetRendererIntensity(mainGlowRenderer, mainGlowIntensity * multiplier);
        SetRendererIntensity(mainCoreRenderer, mainCoreIntensity * multiplier);

        if (branchRenderers != null)
        {
            foreach (LightningBranchRenderer branch in branchRenderers)
            {
                if (branch == null)
                    continue;

                SetRendererIntensity(branch.glowRenderer, mainGlowIntensity * branchIntensityMultiplier * multiplier);
                SetRendererIntensity(branch.coreRenderer, mainCoreIntensity * branchIntensityMultiplier * multiplier);
            }
        }

        ApplySceneLightIntensity(multiplier);
    }

    private void SetRendererIntensity(LineRenderer lineRenderer, float intensity)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.GetPropertyBlock(materialProperties);
        materialProperties.SetFloat(ShaderIntensityId, intensity);
        lineRenderer.SetPropertyBlock(materialProperties);
    }

    private void SetBoltRenderersEnabled(bool enabled)
    {
        if (mainGlowRenderer != null)
            mainGlowRenderer.enabled = enabled;

        if (mainCoreRenderer != null)
            mainCoreRenderer.enabled = enabled;

        if (branchRenderers == null)
            return;

        foreach (LightningBranchRenderer branch in branchRenderers)
        {
            if (branch == null)
                continue;

            SetBranchRendererEnabled(branch, enabled && branch.isVisible);
        }
    }

    private void SetAllBranchRenderersEnabled(bool enabled)
    {
        if (branchRenderers == null)
            return;

        foreach (LightningBranchRenderer branch in branchRenderers)
        {
            if (branch == null)
                continue;

            branch.isVisible = enabled;
            SetBranchRendererEnabled(branch, enabled);
        }
    }

    private static void SetBranchRendererEnabled(LightningBranchRenderer branch, bool enabled)
    {
        if (branch == null)
            return;

        if (branch.glowRenderer != null)
            branch.glowRenderer.enabled = enabled;

        if (branch.coreRenderer != null)
            branch.coreRenderer.enabled = enabled;
    }

    private static void CreatePerpendicularAxes(Vector3 direction, out Vector3 perpendicular, out Vector3 binormal)
    {
        Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
        perpendicular = Vector3.Cross(direction, referenceAxis).normalized;
        binormal = Vector3.Cross(direction, perpendicular).normalized;
    }
}
