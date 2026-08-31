using UnityEngine;

public partial class ProceduralLightning
{
    private void ResolveTargetSurface(Vector3 startPosition, Vector3 desiredEndPosition, out Vector3 resolvedEndPosition, out Vector3 resolvedNormal)
    {
        currentImpactTransform = null;

        Vector3 direction = desiredEndPosition - startPosition;
        float distance = direction.magnitude;

        if (distance <= Mathf.Epsilon)
        {
            resolvedEndPosition = startPosition;
            resolvedNormal = Vector3.up;
            return;
        }

        direction /= distance;

        float detectionDistance = distance;

        if (useExternalEndPoint)
            detectionDistance += externalEndPointDetectionPadding;

        if (detectSurfaces && Physics.Raycast(startPosition, direction, out RaycastHit hit, detectionDistance, impactLayers, QueryTriggerInteraction.Ignore))
        {
            currentImpactTransform = hit.collider.transform;
            resolvedEndPosition = hit.point;
            resolvedNormal = hit.normal;
            return;
        }

        resolvedEndPosition = desiredEndPosition;
        resolvedNormal = -direction;
    }

    private void UpdateImpactTransform()
    {
        if (impactTransform != null)
        {
            impactTransform.position = currentImpactPosition + currentImpactNormal * impactSurfaceOffset;

            if (alignImpactToSurface)
                impactTransform.rotation = Quaternion.LookRotation(currentImpactNormal);
        }

        if (audioSource != null)
            audioSource.transform.position = currentImpactPosition;
    }

    private void PlayImpactParticles()
    {
        UpdateImpactTransform();
        RestartParticleSystem(impactFlashParticles);
        RestartParticleSystem(impactSparkParticles);

        if (currentImpactTransform != null)
            BeginSurfacePropagation(currentImpactTransform, currentImpactPosition, currentImpactNormal);
    }

    private static void RestartParticleSystem(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
            return;

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleSystem.Play(true);
    }

    private void ApplySceneLightIntensity(float multiplier)
    {
        if (sceneLight == null)
            return;

        float baseIntensity = playbackMode == LightningMode.Continuous ? continuousLightIntensity : burstLightIntensity;
        sceneLight.intensity = baseIntensity * multiplier;
        sceneLight.enabled = multiplier > 0f;
    }

    private void PlayConfiguredAudio()
    {
        if (audioSource == null)
            return;

        audioSource.Stop();
        audioSource.volume = audioVolume;

        if (playbackMode == LightningMode.Burst)
        {
            if (burstAudioClip == null)
                return;

            audioSource.loop = false;
            audioSource.pitch = Random.Range(burstPitchRange.x, burstPitchRange.y);
            audioSource.PlayOneShot(burstAudioClip);
            return;
        }

        if (continuousAudioClip == null)
            return;

        audioSource.clip = continuousAudioClip;
        audioSource.loop = true;
        audioSource.pitch = 1f;
        audioSource.Play();
    }
}
