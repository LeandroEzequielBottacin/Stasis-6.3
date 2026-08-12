using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if CINEMACHINE
using Cinemachine;
#endif

namespace _Ian.PP_Profiles
{
    [RequireComponent(typeof(BoxCollider))]
    public class JumpPadVolumeKickPlus : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Volume local del pad con overrides (Bloom/CA/LensDist/etc.)")]
        public Volume padVolume;
        [Tooltip("Tag del jugador para disparar el efecto")]
        public string playerTag = "Player";

        [Header("Weights")]
        [Range(0,1)] public float peakWeight = 1f;
        [Range(0,1)] public float holdWeight = 0.65f;
        [Range(0,1)] public float outsideWeight = 0f;

        [Header("Timings (unscaled)")]
        public float riseTime = 0.10f;      // subida a pico
        public float peakHoldTime = 0.05f;  // mantener en pico
        public float fallTime = 0.22f;      // caer a hold
        public float exitTime = 0.18f;      // al salir a outsideWeight

        [Header("Curvas (0..1)")]
        public AnimationCurve upCurve = AnimationCurve.EaseInOut(0,0,1,1);
        public AnimationCurve downCurve = AnimationCurve.EaseInOut(0,0,1,1);

        [Header("Chromatic Aberration (pulse)")]
        public bool pulseChromaticAberration = true;
        [Range(0,1)] public float caPeak = 0.18f;
        [Range(0,1)] public float caHold = 0.06f;

        [Header("Chromatic Aberration Burst (rápido)")]
        public bool doCaBurst = true;
        [Range(0,1)] public float caBurstPeak = 0.45f;   // pico extra (además de caPeak)
        public float caBurstRise = 0.04f;                 // ~40 ms
        public float caBurstFall = 0.12f;                 // ~120 ms
        public AnimationCurve caBurstUp = AnimationCurve.EaseInOut(0,0,1,1);
        public AnimationCurve caBurstDown = AnimationCurve.EaseInOut(0,1,1,0);
        [Range(0f, 120f)] public float caBurstJitterHz = 35f;
        [Range(0f, 0.3f)] public float caBurstJitterAmount = 0.06f;
        Coroutine caBurstCo;

        [Header("Lens Distortion (pulse)")]
        public bool pulseLensDistortion = true;
        [Range(-1,1)] public float ldPeak = -0.36f;
        [Range(-1,1)] public float ldHold = -0.14f;

        [Header("Lens Dist (extra)")]
        [Tooltip("Amplía la distorsión periférica (URP: 'Scale')")]
        public bool animateLdScale = true;
        [Range(0.8f, 2f)] public float ldScalePeak = 1.28f;
        [Range(0.8f, 2f)] public float ldScaleHold = 1.06f;
        [Tooltip("Empuja el centro vertical en el pico (estirón hacia abajo)")]
        public bool animateLdCenter = true;
        [Range(-0.5f, 0.5f)] public float ldCenterYPeak = -0.06f;

        [Header("Bloom (pulse opcional)")]
        public bool pulseBloom = false;
        [Range(0f, 1.5f)] public float bloomPeak = 0.85f;
        [Range(0f, 1.5f)] public float bloomHold = 0.48f;
        [Range(0.5f, 2.0f)] public float bloomThresholdAtPeak = 0.95f;
        [Range(0.5f, 2.0f)] public float bloomThresholdAtHold = 1.10f;

        [Header("Camera FOV Punch")]
        public bool doFovPunch = true;
        public float fovPeakDelta = 14f;     // cuánto subir el FOV en el pico
        public float fovRecoverTime = 0.34f; // tiempo total de recuperación

#if CINEMACHINE
    [Tooltip("Si se deja vacío, tomará la vcam activa desde el CinemachineBrain")]
    public CinemachineVirtualCamera vcam;
#endif

        [Header("Cinemachine Impulse (shake opcional)")]
        public bool doImpulse = true;
#if CINEMACHINE
    public CinemachineImpulseSource impulseSource;
#endif

        [Header("Control")]
        public float cooldown = 0.15f;   // tiempo mínimo entre kicks

        // Internos (volumes)
        ChromaticAberration ca;
        LensDistortion ld;
        Bloom bloom;

        // Defaults para restaurar
        float bloomBaseIntensity, bloomBaseThreshold;
        bool bloomDefaultsCached;
        Vector2 ldBaseCenter = Vector2.zero;
        float ldBaseScale = 1f;
        bool ldDefaultsCached;

        // FOV
        float baseFov = -1f;
        Camera cachedMainCam;

        Coroutine anim;
        float lastKickTime = -999f;

        void Reset() {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            padVolume = GetComponent<Volume>();
        }

        void Awake() {
            if (padVolume && padVolume.profile) {
                padVolume.profile.TryGet(out ca);
                padVolume.profile.TryGet(out ld);
                padVolume.profile.TryGet(out bloom);

                if (bloom) {
                    bloomBaseIntensity = bloom.intensity.value;
                    bloomBaseThreshold = bloom.threshold.value;
                    bloomDefaultsCached = true;
                }
                if (ld) {
                    ldBaseCenter = ld.center.value;
                    ldBaseScale = ld.scale.value;
                    ldDefaultsCached = true;
                }
            }
            cachedMainCam = Camera.main;
        }

        void OnDisable() {
            RestoreFovImmediate();
            RestoreBloomDefaults();
            RestoreLensDistDefaults();
            if (caBurstCo != null) { StopCoroutine(caBurstCo); caBurstCo = null; }
        }

        void OnTriggerEnter(Collider other) {
            if (!MatchesPlayer(other)) return;
            if (Time.unscaledTime - lastKickTime < cooldown) return;
            lastKickTime = Time.unscaledTime;

            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(KickRoutine());

            FireImpulse();
            PunchFov();

            if (doCaBurst && ca != null) {
                if (caBurstCo != null) { StopCoroutine(caBurstCo); caBurstCo = null; }
                // Prepara CA al menos en el nivel de pico base
                if (pulseChromaticAberration) ca.intensity.value = Mathf.Max(ca.intensity.value, caPeak);
                caBurstCo = StartCoroutine(ChromaticBurst());
            }
        }

        void OnTriggerExit(Collider other) {
            if (!MatchesPlayer(other)) return;

            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(LerpWeight(padVolume ? padVolume.weight : 0f, outsideWeight, exitTime, AnimationCurve.EaseInOut(0,0,1,1)));

            // Limpiar efectos
            if (caBurstCo != null) { StopCoroutine(caBurstCo); caBurstCo = null; }
            if (ca) ca.intensity.value = 0f;
            RestoreLensDistDefaults();
            RestoreBloomHold(); // o defaults si no hay pulseBloom
        }

        bool MatchesPlayer(Collider c) => string.IsNullOrEmpty(playerTag) || c.CompareTag(playerTag);

        IEnumerator KickRoutine() {
            if (!padVolume) yield break;

            // 1) Subir a pico
            yield return LerpWeight(padVolume.weight, peakWeight, riseTime, upCurve);
            ApplyPulses(atPeak: true);

            // 2) Mantener en pico
            if (peakHoldTime > 0f) {
                float t = 0f;
                while (t < peakHoldTime) { t += Time.unscaledDeltaTime; yield return null; }
            }

            // 3) Caer a hold
            yield return LerpWeight(peakWeight, holdWeight, fallTime, downCurve);
            ApplyPulses(atPeak: false);

            anim = null;
        }

        IEnumerator LerpWeight(float a, float b, float duration, AnimationCurve curve) {
            float t = 0f;
            while (t < duration) {
                t += Time.unscaledDeltaTime;
                float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
                float c = (curve != null) ? Mathf.Clamp01(curve.Evaluate(k)) : k;
                if (padVolume) padVolume.weight = Mathf.LerpUnclamped(a, b, c);
                yield return null;
            }
            if (padVolume) padVolume.weight = b;
        }

        void ApplyPulses(bool atPeak) {
            // CA
            if (pulseChromaticAberration && ca) {
                ca.intensity.value = atPeak ? caPeak : caHold;
            }
            // Lens Distortion
            if (pulseLensDistortion && ld) {
                ld.intensity.value = atPeak ? ldPeak : ldHold;
                if (animateLdScale) {
                    ld.scale.value = atPeak ? ldScalePeak : ldScaleHold;
                }
                if (animateLdCenter) {
                    var c = ld.center.value;
                    c.y = atPeak ? ldCenterYPeak : (ldDefaultsCached ? ldBaseCenter.y : 0f);
                    ld.center.value = c;
                }
            }
            // Bloom
            if (pulseBloom && bloom) {
                bloom.intensity.value = atPeak ? bloomPeak : bloomHold;
                bloom.threshold.value = atPeak ? bloomThresholdAtPeak : bloomThresholdAtHold;
            }
        }

        void RestoreBloomDefaults() {
            if (!bloom) return;
            if (bloomDefaultsCached) {
                bloom.intensity.value = bloomBaseIntensity;
                bloom.threshold.value = bloomBaseThreshold;
            } else {
                bloom.intensity.value = bloomHold;
                bloom.threshold.value = bloomThresholdAtHold;
            }
        }

        void RestoreBloomHold() {
            if (!bloom) return;
            if (pulseBloom) {
                bloom.intensity.value = bloomHold;
                bloom.threshold.value = bloomThresholdAtHold;
            } else {
                RestoreBloomDefaults();
            }
        }

        void RestoreLensDistDefaults() {
            if (!ld) return;
            ld.intensity.value = 0f;
            if (ldDefaultsCached) {
                ld.scale.value = ldBaseScale;
                ld.center.value = ldBaseCenter;
            } else {
                ld.scale.value = 1f;
                ld.center.value = Vector2.zero;
            }
        }

        // =========================
        // Chromatic Aberration Burst
        // =========================
        IEnumerator ChromaticBurst() {
            if (ca == null) yield break;

            float baseAtPeak = Mathf.Clamp01(ca.intensity.value);
            float target = Mathf.Clamp01(Mathf.Max(baseAtPeak, caBurstPeak));

            // Subida rápida
            float t = 0f;
            while (t < caBurstRise) {
                t += Time.unscaledDeltaTime;
                float k = caBurstRise > 0f ? Mathf.Clamp01(t / caBurstRise) : 1f;
                float c = caBurstUp != null ? Mathf.Clamp01(caBurstUp.Evaluate(k)) : k;

                float jitter = 0f;
                if (caBurstJitterHz > 0f && caBurstJitterAmount > 0f) {
                    jitter = Mathf.Sin(t * Mathf.PI * 2f * caBurstJitterHz) * caBurstJitterAmount;
                }
                ca.intensity.value = Mathf.Clamp01(Mathf.Lerp(baseAtPeak, target, c) + jitter);
                yield return null;
            }

            // Caída a Hold
            t = 0f;
            while (t < caBurstFall) {
                t += Time.unscaledDeltaTime;
                float k = caBurstFall > 0f ? Mathf.Clamp01(t / caBurstFall) : 1f;
                float c = caBurstDown != null ? Mathf.Clamp01(caBurstDown.Evaluate(k)) : (1f - k);

                float jitter = 0f;
                if (caBurstJitterHz > 0f && caBurstJitterAmount > 0f) {
                    jitter = Mathf.Sin((caBurstRise + t) * Mathf.PI * 2f * caBurstJitterHz) * (caBurstJitterAmount * 0.5f);
                }
                float holdTarget = Mathf.Clamp01(caHold);
                ca.intensity.value = Mathf.Clamp01(Mathf.Lerp(target, holdTarget, 1f - c) + jitter);
                yield return null;
            }
        }

        // =========================
        // FOV Punch con overshoot
        // =========================
        void PunchFov() {
            if (!doFovPunch) return;

#if CINEMACHINE
        CinemachineVirtualCamera camToUse = vcam;
        if (camToUse == null) {
            var brain = FindObjectOfType<CinemachineBrain>();
            if (brain && brain.ActiveVirtualCamera is CinemachineVirtualCamera v) camToUse = v;
        }
        if (camToUse != null) {
            if (baseFov < 0f) baseFov = camToUse.m_Lens.FieldOfView;
            float target = baseFov + Mathf.Abs(fovPeakDelta);
            StartCoroutine(RecoverFovCM(camToUse, target));
            return;
        }
#endif
            if (cachedMainCam) {
                if (baseFov < 0f) baseFov = cachedMainCam.fieldOfView;
                float target = baseFov + Mathf.Abs(fovPeakDelta);
                StartCoroutine(RecoverFovMain(cachedMainCam, target));
            }
        }

        void RestoreFovImmediate() {
#if CINEMACHINE
        if (vcam && baseFov > 0f) vcam.m_Lens.FieldOfView = baseFov;
#endif
            if (cachedMainCam && baseFov > 0f) cachedMainCam.fieldOfView = baseFov;
            baseFov = -1f;
        }

#if CINEMACHINE
    IEnumerator RecoverFovCM(CinemachineVirtualCamera cam, float blownFov) {
        float undershoot = baseFov - Mathf.Abs(fovPeakDelta) * 0.15f;
        float t1 = fovRecoverTime * 0.65f;
        float t = 0f;
        cam.m_Lens.FieldOfView = blownFov;
        while (t < t1) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / t1);
            cam.m_Lens.FieldOfView = Mathf.Lerp(blownFov, undershoot, 1f - (1f - k) * (1f - k)); // ease-out
            yield return null;
        }
        float t2 = fovRecoverTime * 0.35f;
        t = 0f;
        while (t < t2) {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / t2);
            cam.m_Lens.FieldOfView = Mathf.Lerp(undershoot, baseFov, k * (2f - k)); // ease-in-out
            yield return null;
        }
        cam.m_Lens.FieldOfView = baseFov;
    }
#endif

        IEnumerator RecoverFovMain(Camera cam, float blownFov) {
            float undershoot = baseFov - Mathf.Abs(fovPeakDelta) * 0.15f;
            float t1 = fovRecoverTime * 0.65f;
            float t = 0f;
            cam.fieldOfView = blownFov;
            while (t < t1) {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / t1);
                cam.fieldOfView = Mathf.Lerp(blownFov, undershoot, 1f - (1f - k) * (1f - k)); // ease-out
                yield return null;
            }
            float t2 = fovRecoverTime * 0.35f;
            t = 0f;
            while (t < t2) {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / t2);
                cam.fieldOfView = Mathf.Lerp(undershoot, baseFov, k * (2f - k)); // ease-in-out
                yield return null;
            }
            cam.fieldOfView = baseFov;
        }

        // =========================
        // Impulse opcional
        // =========================
        void FireImpulse() {
#if CINEMACHINE
        if (!doImpulse) return;
        if (impulseSource != null) {
            impulseSource.GenerateImpulse();
        }
#endif
        }
    }
}
