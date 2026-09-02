using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class LightningBranchRenderer
{
    [Tooltip("LineRenderer del halo exterior de esta ramificacion. Usa el grosor y brillo configurados para las ramas.")]
    [FormerlySerializedAs("glowLine")] public LineRenderer glowRenderer;
    [Tooltip("LineRenderer del nucleo fino de esta ramificacion. Usa el grosor y brillo configurados para las ramas.")]
    [FormerlySerializedAs("coreLine")] public LineRenderer coreRenderer;

    [Tooltip("Puntos mundiales calculados para esta ramificacion. Buffer interno no serializado.")]
    [System.NonSerialized] public Vector3[] positions;
    [Tooltip("Estado interno no serializado que indica si esta ramificacion fue seleccionada para mostrarse.")]
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
    [Tooltip("Transform de origen del rayo. Play con posiciones modifica su posicion; debe estar asignado tambien en ese modo.")]
    [SerializeField] private Transform sourceTransform;

    [FormerlySerializedAs("endPoint")]
    [Tooltip("Transform de destino para Play sin parametros. No se usa como destino mientras este activo un punto final externo.")]
    [SerializeField] private Transform targetTransform;

    [Header("Main Bolt Renderers")]
    [FormerlySerializedAs("glowLine")]
    [Tooltip("LineRenderer que dibuja el halo exterior del rayo principal.")]
    [SerializeField] private LineRenderer mainGlowRenderer;

    [FormerlySerializedAs("coreLine")]
    [Tooltip("LineRenderer que dibuja el nucleo fino y luminoso del rayo principal.")]
    [SerializeField] private LineRenderer mainCoreRenderer;

    [Header("Branch Renderers")]
    [FormerlySerializedAs("branches")]
    [Tooltip("Pares de LineRenderer disponibles para las ramificaciones. Cada elemento permite dibujar una rama; su visibilidad depende de Branch Visible Chance.")]
    [SerializeField] private LightningBranchRenderer[] branchRenderers;

    [Header("Scene Light Reference")]
    [FormerlySerializedAs("flashLight")]
    [Tooltip("Luz opcional iluminada durante la reproduccion del rayo. Su intensidad depende del modo y del brillo actual.")]
    [SerializeField] private Light sceneLight;

    [Header("Impact References")]
    [FormerlySerializedAs("impactRoot")]
    [Tooltip("Objeto que posiciona los efectos de impacto. Se desplaza segun Impact Surface Offset y puede orientarse con la normal.")]
    [SerializeField] private Transform impactTransform;

    [FormerlySerializedAs("impactFlash")]
    [Tooltip("Sistema opcional del destello de impacto. Se limpia y reinicia al reproducir los efectos de impacto.")]
    [SerializeField] private ParticleSystem impactFlashParticles;

    [FormerlySerializedAs("impactSparks")]
    [Tooltip("Sistema opcional de chispas de impacto. Se limpia y reinicia al reproducir los efectos de impacto.")]
    [SerializeField] private ParticleSystem impactSparkParticles;

    [Header("Audio References")]
    [FormerlySerializedAs("lightningAudioSource")]
    [Tooltip("AudioSource opcional para los sonidos del rayo. Su posicion se actualiza al punto de impacto.")]
    [SerializeField] private AudioSource audioSource;

    [FormerlySerializedAs("burstSound")]
    [Tooltip("Sonido reproducido una vez al iniciar un rayo en modo Burst.")]
    [SerializeField] private AudioClip burstAudioClip;

    [FormerlySerializedAs("continuousSound")]
    [Tooltip("Sonido reproducido en bucle mientras esta activo el modo Continuous.")]
    [SerializeField] private AudioClip continuousAudioClip;

    [Header("Main Bolt Shape")]
    [FormerlySerializedAs("segmentCount")]
    [Min(2)]
    [Tooltip("Cantidad base de segmentos del rayo principal. Mas segmentos permiten mas detalle; en superficies el refinamiento puede anadir puntos adicionales.")]
    [SerializeField] private int mainSegmentCount = 20;

    [FormerlySerializedAs("displacement")]
    [Min(0f)]
    [Tooltip("Amplitud del desplazamiento lateral del rayo principal libre. En superficies se combina con Surface Main Roughness para definir la irregularidad.")]
    [SerializeField] private float mainDisplacement = 0.35f;

    [FormerlySerializedAs("widthCurve")]
    [SerializeField]
    [Tooltip("Perfil de grosor a lo largo del rayo principal: X va de origen 0 a destino 1; Y multiplica los grosores de Glow y Core.")]
    private AnimationCurve mainWidthCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.05f, 1f),
        new Keyframe(0.95f, 1f),
        new Keyframe(1f, 0f)
    );


    [Header("Surface Detection")]
    [Tooltip("Activa el raycast entre origen y destino para resolver el impacto. Permite identificar la superficie desde la que comienza la propagacion electrica.")]
    [SerializeField] private bool detectSurfaces = true;
    [Tooltip("Capas que puede detectar el raycast interno de ProceduralLightning. Los triggers se ignoran.")]
    [SerializeField] private LayerMask impactLayers = ~0;

    [Min(0f)]
    [Tooltip("Separacion del objeto de efectos de impacto en la direccion de la normal, en unidades de mundo. No modifica el extremo geometrico del rayo.")]
    [SerializeField] private float impactSurfaceOffset = 0.02f;

    [Tooltip("Orienta el eje Z del objeto de impacto en la direccion de la normal de la superficie.")]
    [SerializeField] private bool alignImpactToSurface = true;

    [Min(0f)]
    [Tooltip("Distancia adicional del raycast cuando se usa un punto final externo. Ayuda a detectar el collider situado justo en ese extremo.")]
    [SerializeField] private float externalEndPointDetectionPadding = 0.1f;


    [Header("Main Bolt Widths")]
    [FormerlySerializedAs("glowWidth")]
    [Min(0.001f)]
    [Tooltip("Grosor base del halo exterior del rayo principal, multiplicado por Main Width Curve. No controla las ramificaciones.")]
    [SerializeField] private float mainGlowWidth = 0.12f;

    [FormerlySerializedAs("coreWidth")]
    [Min(0.001f)]
    [Tooltip("Grosor base del nucleo del rayo principal, multiplicado por Main Width Curve. No controla las ramificaciones.")]
    [SerializeField] private float mainCoreWidth = 0.025f;

    [Header("Playback")]
    [FormerlySerializedAs("mode")]
    [Tooltip("Continuous mantiene el rayo activo hasta Stop; Burst reproduce una descarga limitada por Burst Duration. Las instancias de superficie usan su propia actualizacion con esa duracion.")]
    [SerializeField] private LightningMode playbackMode = LightningMode.Continuous;
    [Tooltip("Inicia Play automaticamente en Start para rayos normales. Las instancias de superficie omiten ese arranque.")]
    [SerializeField] private bool playOnAwake = true;

    [Header("Continuous Playback")]
    [FormerlySerializedAs("refreshInterval")]
    [Min(0.001f)]
    [Tooltip("Tiempo entre regeneraciones de la geometria en modo Continuous, en segundos. Menor intervalo produce cambios mas frecuentes.")]
    [SerializeField] private float continuousRefreshInterval = 0.03f;

    [Header("Burst Playback")]
    [Min(0.01f)]
    [Tooltip("Duracion de la descarga Burst y de cada instancia superficial, en segundos. Al finalizar, la instancia superficial vuelve a su pool si tiene uno asignado.")]
    [SerializeField] private float burstDuration = 0.25f;

    [FormerlySerializedAs("flickerInterval")]
    [Min(0.001f)]
    [Tooltip("Tiempo entre cambios de visibilidad y geometria de la descarga Burst, en segundos.")]
    [SerializeField] private float burstFlickerInterval = 0.035f;

    [FormerlySerializedAs("flickerVisibleChance")]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de mostrar el rayo en cada cambio de parpadeo Burst: 0 nunca y 1 siempre en esos cambios.")]
    [SerializeField] private float burstVisibleChance = 0.75f;

    [SerializeField]
    [Tooltip("Evolucion del brillo durante Burst y los rayos superficiales. X es el progreso de vida de 0 a 1; Y multiplica la intensidad.")]
    private AnimationCurve burstIntensityCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.15f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Shader Intensity")]
    [FormerlySerializedAs("glowIntensity")]
    [Min(0f)]
    [Tooltip("Intensidad base de la propiedad _Intensity del halo principal. Tambien sirve como base del brillo de los halos de las ramas.")]
    [SerializeField] private float mainGlowIntensity = 5f;

    [FormerlySerializedAs("coreIntensity")]
    [Min(0f)]
    [Tooltip("Intensidad base de la propiedad _Intensity del nucleo principal. Tambien sirve como base del brillo de los nucleos de las ramas.")]
    [SerializeField] private float mainCoreIntensity = 8f;

    [Range(0f, 1f)]
    [Tooltip("Multiplicador del brillo de las ramificaciones respecto de las intensidades principales. Con 0 sus intensidades quedan en cero.")]
    [SerializeField] private float branchIntensityMultiplier = 0.75f;

    [Header("Main Bolt Noise")]
    [Min(0.01f)]
    [Tooltip("Escala espacial del ruido Perlin a lo largo del rayo principal libre. Valores mayores producen variaciones mas frecuentes en su longitud.")]
    [SerializeField] private float noiseScale = 1.5f;

    [Min(0f)]
    [Tooltip("Velocidad temporal del ruido Perlin del rayo libre. Con 0 se detiene ese ruido, pero continua la parte aleatoria de cada regeneracion.")]
    [SerializeField] private float noiseSpeed = 8f;

    [Tooltip("Desplazamiento del muestreo Perlin y semilla adicional de las superficies. No fija por si solo toda la aleatoriedad del rayo libre.")]
    [SerializeField] private int noiseSeed = 12345;

    [Header("Branch Shape")]
    [Min(2)]
    [Tooltip("Cantidad base de segmentos por ramificacion. El refinamiento superficial puede anadir puntos adicionales.")]
    [SerializeField] private int branchSegmentCount = 8;

    [Range(0.05f, 1f)]
    [Tooltip("Proporcion minima de longitud de una rama respecto del rayo libre. En superficies participa en el calculo de cuanto envuelve la rama al collider.")]
    [SerializeField] private float minimumBranchLengthRatio = 0.15f;

    [Range(0.05f, 1f)]
    [Tooltip("Proporcion maxima de longitud de una rama respecto del rayo libre. Debe ser igual o mayor que el minimo; tambien interviene en la envoltura superficial.")]
    [SerializeField] private float maximumBranchLengthRatio = 0.35f;

    [Range(0f, 2f)]
    [Tooltip("Influencia de la direccion del rayo principal sobre las ramas libres. Valores mayores las orientan mas hacia el destino.")]
    [SerializeField] private float branchForwardInfluence = 0.35f;

    [Min(0f)]
    [Tooltip("Irregularidad lateral de las ramas libres y amplitud de la perturbacion direccional de las ramas proyectadas sobre superficies.")]
    [SerializeField] private float branchDisplacement = 0.15f;

    [Min(0.001f)]
    [Tooltip("Grosor base del halo de las ramificaciones, multiplicado por Branch Width Curve.")]
    [SerializeField] private float branchGlowWidth = 0.06f;

    [Min(0.001f)]
    [Tooltip("Grosor base del nucleo de las ramificaciones, multiplicado por Branch Width Curve.")]
    [SerializeField] private float branchCoreWidth = 0.012f;

    [Range(0f, 1f)]
    [Tooltip("Primer punto relativo permitido para el nacimiento de ramas: 0 corresponde al origen y 1 al final. El indice se limita a puntos interiores.")]
    [SerializeField] private float minimumBranchStart = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Ultimo punto relativo permitido para el nacimiento de ramas. Debe ser igual o mayor que el minimo; el indice se limita a puntos interiores.")]
    [SerializeField] private float maximumBranchStart = 0.85f;

    [SerializeField]
    [Tooltip("Perfil de grosor de las ramificaciones: X va desde su nacimiento 0 hasta su extremo 1 e Y multiplica sus grosores.")]
    private AnimationCurve branchWidthCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0f)
    );
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de mostrar cada ramificacion al regenerar la geometria: 0 casi nunca y 1 siempre.")]
    [SerializeField] private float branchVisibleChance = 0.65f;

    [Header("Scene Lighting")]
    [Min(0f)]
    [Tooltip("Intensidad base de Scene Light cuando el modo seleccionado es Continuous.")]
    [SerializeField] private float continuousLightIntensity = 2f;

    [Min(0f)]
    [Tooltip("Intensidad base de Scene Light cuando el modo seleccionado es Burst, multiplicada por la intensidad visual actual.")]
    [SerializeField] private float burstLightIntensity = 8f;

    [Tooltip("Mueve Scene Light al punto medio del rayo cuando se genera la geometria del rayo libre.")]
    [SerializeField] private bool positionLightAtMidpoint = true;


    [Header("Audio Settings")]
    [Range(0f, 1f)]
    [Tooltip("Volumen del AudioSource al iniciar la reproduccion, entre 0 y 1.")]
    [SerializeField] private float audioVolume = 1f;

    [Tooltip("Rango aleatorio de tono del sonido Burst: X es el minimo e Y el maximo. El valor 1 conserva el tono original.")]
    [SerializeField] private Vector2 burstPitchRange = new Vector2(0.9f, 1.1f);


    [Tooltip("Identificador compartido de la propiedad _Intensity del shader, usado al actualizar los renderers.")]
    private static readonly int ShaderIntensityId = Shader.PropertyToID("_Intensity");

    [Tooltip("Bloque reutilizado para modificar la intensidad de los renderers sin alterar sus materiales compartidos.")]
    private MaterialPropertyBlock materialProperties;
    [Tooltip("Buffer de posiciones mundiales del rayo principal, compartido con los modos de superficie y carga.")]
    private Vector3[] mainPositions;
    [Tooltip("Cuenta regresiva, en segundos, hasta la proxima regeneracion del rayo continuo.")]
    private float continuousRefreshTimer;
    [Tooltip("Tiempo transcurrido de la descarga Burst actual, en segundos.")]
    private float burstElapsedTime;
    [Tooltip("Cuenta regresiva, en segundos, hasta el proximo cambio de parpadeo Burst.")]
    private float burstFlickerTimer;
    [Tooltip("Estado interno: indica que el rayo esta reproduciendose y debe actualizarse.")]
    private bool isPlaying;
    [Tooltip("Estado interno que controla la visibilidad actual del rayo principal y condiciona las ramas del rayo libre.")]
    private bool isMainBoltVisible;
    [Tooltip("Punto mundial de impacto o destino resuelto en la ultima generacion del rayo.")]
    private Vector3 currentImpactPosition;
    [Tooltip("Normal del ultimo impacto; si no hubo colision, se usa la direccion opuesta al rayo.")]
    private Vector3 currentImpactNormal;
    [Tooltip("Transform del collider detectado por el raycast interno; queda nulo cuando no se detecta una superficie.")]
    private Transform currentImpactTransform;

    [Tooltip("Indica que el destino proviene de Play con posiciones y no de Target Transform.")]
    private bool useExternalEndPoint;
    [Tooltip("Posicion mundial del destino externo guardado por Play con posiciones.")]
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
