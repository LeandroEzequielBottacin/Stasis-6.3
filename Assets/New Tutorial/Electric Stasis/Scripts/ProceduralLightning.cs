using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class LightningBranchRenderer
{
    [FormerlySerializedAs("glowLine")] public LineRenderer glowRenderer;
    [FormerlySerializedAs("coreLine")] public LineRenderer coreRenderer;

    [System.NonSerialized] public Vector3[] positions;
    [System.NonSerialized] public bool isVisible;
}

public partial class ProceduralLightning : MonoBehaviour
{
    public enum LightningMode
    {
        Continuous,
        Burst
    }

    [Header("Endpoints")]
    [FormerlySerializedAs("startPoint")]
    [SerializeField] private Transform sourceTransform;

    [FormerlySerializedAs("endPoint")]
    [SerializeField] private Transform targetTransform;

    [Header("Main Bolt Renderers")]
    [FormerlySerializedAs("glowLine")]
    [SerializeField] private LineRenderer mainGlowRenderer;

    [FormerlySerializedAs("coreLine")]
    [SerializeField] private LineRenderer mainCoreRenderer;

    [Header("Branch Renderers")]
    [FormerlySerializedAs("branches")]
    [SerializeField] private LightningBranchRenderer[] branchRenderers;

    [Header("Scene Light Reference")]
    [FormerlySerializedAs("flashLight")]
    [SerializeField] private Light sceneLight;

    [Header("Impact References")]
    [FormerlySerializedAs("impactRoot")]
    [SerializeField] private Transform impactTransform;

    [FormerlySerializedAs("impactFlash")]
    [SerializeField] private ParticleSystem impactFlashParticles;

    [FormerlySerializedAs("impactSparks")]
    [SerializeField] private ParticleSystem impactSparkParticles;

    [Header("Audio References")]
    [FormerlySerializedAs("lightningAudioSource")]
    [SerializeField] private AudioSource audioSource;

    [FormerlySerializedAs("burstSound")]
    [SerializeField] private AudioClip burstAudioClip;

    [FormerlySerializedAs("continuousSound")]
    [SerializeField] private AudioClip continuousAudioClip;

    [Header("Main Bolt Shape")]
    [FormerlySerializedAs("segmentCount")]
    [Min(2)]
    [SerializeField] private int mainSegmentCount = 20;

    [FormerlySerializedAs("displacement")]
    [Min(0f)]
    [SerializeField] private float mainDisplacement = 0.35f;

    [FormerlySerializedAs("widthCurve")]
    [SerializeField]
    private AnimationCurve mainWidthCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.05f, 1f),
        new Keyframe(0.95f, 1f),
        new Keyframe(1f, 0f)
    );


    [Header("Surface Detection")]
    [SerializeField] private bool detectSurfaces = true;
    [SerializeField] private LayerMask impactLayers = ~0;

    [Min(0f)]
    [SerializeField] private float impactSurfaceOffset = 0.02f;

    [SerializeField] private bool alignImpactToSurface = true;

    [Min(0f)]
    [SerializeField] private float externalEndPointDetectionPadding = 0.1f;


    [Header("Main Bolt Widths")]
    [FormerlySerializedAs("glowWidth")]
    [Min(0.001f)]
    [SerializeField] private float mainGlowWidth = 0.12f;

    [FormerlySerializedAs("coreWidth")]
    [Min(0.001f)]
    [SerializeField] private float mainCoreWidth = 0.025f;

    [Header("Playback")]
    [FormerlySerializedAs("mode")]
    [SerializeField] private LightningMode playbackMode = LightningMode.Continuous;
    [SerializeField] private bool playOnAwake = true;

    [Header("Continuous Playback")]
    [FormerlySerializedAs("refreshInterval")]
    [Min(0.001f)]
    [SerializeField] private float continuousRefreshInterval = 0.03f;

    [Header("Burst Playback")]
    [Min(0.01f)]
    [SerializeField] private float burstDuration = 0.25f;

    [FormerlySerializedAs("flickerInterval")]
    [Min(0.001f)]
    [SerializeField] private float burstFlickerInterval = 0.035f;

    [FormerlySerializedAs("flickerVisibleChance")]
    [Range(0f, 1f)]
    [SerializeField] private float burstVisibleChance = 0.75f;

    [SerializeField]
    private AnimationCurve burstIntensityCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.15f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Shader Intensity")]
    [FormerlySerializedAs("glowIntensity")]
    [Min(0f)]
    [SerializeField] private float mainGlowIntensity = 5f;

    [FormerlySerializedAs("coreIntensity")]
    [Min(0f)]
    [SerializeField] private float mainCoreIntensity = 8f;

    [Range(0f, 1f)]
    [SerializeField] private float branchIntensityMultiplier = 0.75f;

    [Header("Main Bolt Noise")]
    [Min(0.01f)]
    [SerializeField] private float noiseScale = 1.5f;

    [Min(0f)]
    [SerializeField] private float noiseSpeed = 8f;

    [SerializeField] private int noiseSeed = 12345;

    [Header("Branch Shape")]
    [Min(2)]
    [SerializeField] private int branchSegmentCount = 8;

    [Range(0.05f, 1f)]
    [SerializeField] private float minimumBranchLengthRatio = 0.15f;

    [Range(0.05f, 1f)]
    [SerializeField] private float maximumBranchLengthRatio = 0.35f;

    [Range(0f, 2f)]
    [SerializeField] private float branchForwardInfluence = 0.35f;

    [Min(0f)]
    [SerializeField] private float branchDisplacement = 0.15f;

    [Min(0.001f)]
    [SerializeField] private float branchGlowWidth = 0.06f;

    [Min(0.001f)]
    [SerializeField] private float branchCoreWidth = 0.012f;

    [Range(0f, 1f)]
    [SerializeField] private float minimumBranchStart = 0.15f;

    [Range(0f, 1f)]
    [SerializeField] private float maximumBranchStart = 0.85f;

    [SerializeField]
    private AnimationCurve branchWidthCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0f)
    );
    [Range(0f, 1f)]
    [SerializeField] private float branchVisibleChance = 0.65f;

    [Header("Scene Lighting")]
    [Min(0f)]
    [SerializeField] private float continuousLightIntensity = 2f;

    [Min(0f)]
    [SerializeField] private float burstLightIntensity = 8f;

    [SerializeField] private bool positionLightAtMidpoint = true;


    [Header("Audio Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float audioVolume = 1f;

    [SerializeField] private Vector2 burstPitchRange = new Vector2(0.9f, 1.1f);


    private static readonly int ShaderIntensityId = Shader.PropertyToID("_Intensity");

    private MaterialPropertyBlock materialProperties;
    private Vector3[] mainPositions;
    private float continuousRefreshTimer;
    private float burstElapsedTime;
    private float burstFlickerTimer;
    private bool isPlaying;
    private bool isMainBoltVisible;
    private Vector3 currentImpactPosition;
    private Vector3 currentImpactNormal;
    private Transform currentImpactTransform;

    private bool useExternalEndPoint;
    private Vector3 currentEndPoint;

    private void Awake()
    {
        materialProperties = new MaterialPropertyBlock();
        InitializeSurfaceLightning();

        if (sceneLight != null)
            sceneLight.enabled = false;
        ConfigureAllLineRenderers();
        RebuildPositionArrays();
        SetBoltRenderersEnabled(false);
    }

    private void Start()
    {
        if (isSurfaceInstance)
            return;

        if (playOnAwake)
            Play();
    }

    private void Update()
    {
        if (isSurfaceInstance)
        {
            UpdateSurfaceInstance();
            return;
        }

        if (!isPlaying)
            return;

        if (sourceTransform == null || (!useExternalEndPoint && targetTransform == null))
        {
            Stop();
            return;
        }

        if (playbackMode == LightningMode.Continuous)
            UpdateContinuous();
        else
            UpdateBurst();
    }

    private void UpdateContinuous()
    {
        if (!isMainBoltVisible)
        {
            isMainBoltVisible = true;
            SetBoltRenderersEnabled(true);
        }

        continuousRefreshTimer -= Time.deltaTime;

        if (continuousRefreshTimer <= 0f)
        {
            GenerateBoltGeometry();
            continuousRefreshTimer = continuousRefreshInterval;
        }

        ApplyVisualIntensity(1f);
    }

    private void UpdateBurst()
    {
        burstElapsedTime += Time.deltaTime;

        if (burstElapsedTime >= burstDuration)
        {
            Stop();
            return;
        }

        burstFlickerTimer -= Time.deltaTime;

        if (burstFlickerTimer <= 0f)
        {
            isMainBoltVisible = Random.value <= burstVisibleChance;
            SetBoltRenderersEnabled(isMainBoltVisible);

            if (isMainBoltVisible)
                GenerateBoltGeometry();

            burstFlickerTimer = burstFlickerInterval;
        }

        float progress = Mathf.Clamp01(burstElapsedTime / burstDuration);
        float intensity = Mathf.Max(0f, burstIntensityCurve.Evaluate(progress));
        ApplyVisualIntensity(isMainBoltVisible ? intensity : 0f);
    }


    public void Play(Vector3 startPoint, Vector3 endPoint)
    {
        if (isSurfaceInstance)
            return;

        if (sourceTransform == null)
        {
            Debug.LogError("ProceduralLightning: Source Transform no esta asignado.", this);
            Stop();
            return;
        }

        sourceTransform.position = startPoint;

        useExternalEndPoint = true;
        currentEndPoint = endPoint;

        isPlaying = true;
        isMainBoltVisible = true;

        continuousRefreshTimer = 0f;
        burstElapsedTime = 0f;
        burstFlickerTimer = burstFlickerInterval;

        SetBoltRenderersEnabled(true);
        GenerateBoltGeometry();

        PlayImpactParticles();

        float initialIntensity =
            playbackMode == LightningMode.Burst
                ? Mathf.Max(0f, burstIntensityCurve.Evaluate(0f))
                : 1f;

        ApplyVisualIntensity(initialIntensity);
        PlayConfiguredAudio();
    }
    public void Play()
    {
        if (isSurfaceInstance)
            return;

        useExternalEndPoint = false;

        if (sourceTransform == null || targetTransform == null)
        {
            Stop();
            return;
        }

        isPlaying = true;
        isMainBoltVisible = true;
        continuousRefreshTimer = 0f;
        burstElapsedTime = 0f;
        burstFlickerTimer = burstFlickerInterval;

        SetBoltRenderersEnabled(true);
        GenerateBoltGeometry();

        PlayImpactParticles();

        float initialIntensity =
            playbackMode == LightningMode.Burst
                ? Mathf.Max(0f, burstIntensityCurve.Evaluate(0f))
                : 1f;

        ApplyVisualIntensity(initialIntensity);
        PlayConfiguredAudio();
    }


    public void Stop()
    {
        isPlaying = false;
        isMainBoltVisible = false;
        ApplyVisualIntensity(0f);
        SetBoltRenderersEnabled(false);

        if (playbackMode == LightningMode.Continuous && audioSource != null)
            audioSource.Stop();
    }

    public void SetEndpoints(Transform source, Transform target)
    {
        sourceTransform = source;
        targetTransform = target;
        useExternalEndPoint = false;
        continuousRefreshTimer = 0f;

        if (isPlaying)
            GenerateBoltGeometry();
    }

    public void SetPoints(Transform start, Transform end)
    {
        SetEndpoints(start, end);
    }

    private void OnValidate()
    {
        mainSegmentCount = Mathf.Max(2, mainSegmentCount);
        branchSegmentCount = Mathf.Max(2, branchSegmentCount);
        continuousRefreshInterval = Mathf.Max(0.001f, continuousRefreshInterval);
        burstDuration = Mathf.Max(0.01f, burstDuration);
        burstFlickerInterval = Mathf.Max(0.001f, burstFlickerInterval);
        mainGlowWidth = Mathf.Max(0.001f, mainGlowWidth);
        mainCoreWidth = Mathf.Max(0.001f, mainCoreWidth);
        branchGlowWidth = Mathf.Max(0.001f, branchGlowWidth);
        branchCoreWidth = Mathf.Max(0.001f, branchCoreWidth);
        maximumBranchLengthRatio = Mathf.Max(minimumBranchLengthRatio, maximumBranchLengthRatio);
        maximumBranchStart = Mathf.Max(minimumBranchStart, maximumBranchStart);
        externalEndPointDetectionPadding = Mathf.Max(0f, externalEndPointDetectionPadding);
        ValidateSurfaceLightningSettings();

        ConfigureAllLineRenderers();
        RebuildPositionArrays();

        if (sourceTransform != null && targetTransform != null)
            GenerateBoltGeometry();
    }
}
