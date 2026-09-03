using Player.Stasis;
using Puzzle_Elements;
using System.Linq;
using UIScripts.FeedBack_UI.Crosshair;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class LaserChargeController : MonoBehaviour
{
    public enum ChargeState
    {
        Idle,
        Charging,
        Ready,
        FinishingIntake
    }

    [Header("Referencias")]
    [Tooltip("SphereCollider que define el centro y radio de la carga. El script lo convierte en trigger y modifica su radio; usa escala uniforme en la jerarquia.")]
    [SerializeField] private SphereCollider energySphere;

    [Tooltip("Esfera visual sin collider, separada del objeto Energy Sphere.")]
    [SerializeField] private Transform energyCore;

    [Tooltip("Renderer del nucleo visual. Se activa durante la carga y recibe la intensidad del shader mediante MaterialPropertyBlock.")]
    [SerializeField] private Renderer coreRenderer;

    [Tooltip("Prefab de ProceduralLightning con referencias internas a sus renderers.")]
    [SerializeField] private ProceduralLightning lightningPrefab;

    [Tooltip("Sistema opcional de particulas que viajan hacia la esfera. El script configura sus modulos y controla la emision y el movimiento.")]
    [SerializeField] private ParticleSystem particlesIn;

    [Tooltip("Luz opcional situada en el centro de la esfera. Su intensidad aumenta con la energia y se apaga al ocultar la carga.")]
    [SerializeField] private Light chargeLight;

    [Header("Tiempo y crecimiento")]
    [Min(0.05f)]
    [Tooltip("Tiempo necesario para completar la carga, en segundos. Al terminar pasa al estado Ready.")]
    [SerializeField] private float chargeDuration = 2.5f;

    [Tooltip("Curva de energia: X es el progreso de carga de 0 a 1 e Y controla tamano, brillo y actividad visual. El resultado se limita a 0 a 1.")]
    [SerializeField] private AnimationCurve energyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Radio local inicial del SphereCollider. Usar escala uniforme en la jerarquia.")]
    [Min(0.01f)]
    [SerializeField] private float minimumRadius = 0.08f;

    [Min(0.01f)]
    [Tooltip("Radio local maximo de la esfera antes del pulso. Un valor mayor agranda el collider y el nucleo visual al finalizar la carga.")]
    [SerializeField] private float maximumRadius = 0.55f;

    [Range(0f, 0.15f)]
    [Tooltip("Amplitud proporcional del pulso de tamano y brillo durante el ultimo 25% de la carga. Por ejemplo, 0.05 equivale a una variacion de hasta el 5%.")]
    [SerializeField] private float finalPulseAmount = 0.05f;

    [Min(0f)]
    [Tooltip("Frecuencia del pulso de tamano y brillo, en ciclos por segundo.")]
    [SerializeField] private float finalPulseFrequency = 12f;

    [Header("Nucleo luminoso")]
    [Tooltip("Nombre de la propiedad Float del shader del nucleo.")]
    [SerializeField] private string coreIntensityProperty = "_Intensity";

    [Tooltip("Intensidad del shader del nucleo: X con energia 0 e Y con energia 1. Tambien se multiplica por el pulso final.")]
    [SerializeField] private Vector2 coreIntensity = new Vector2(0.5f, 15f);

    [Tooltip("Intensidad de la luz de carga: X con energia 0 e Y con energia 1. Tambien se multiplica por el pulso final.")]
    [SerializeField] private Vector2 lightIntensity = new Vector2(0f, 8f);

    [Min(0.01f)]
    [Tooltip("Alcance de la luz de carga, en unidades de mundo.")]
    [SerializeField] private float lightRange = 5f;

    [Header("Rayos sobre la esfera")]
    [Range(1, 16)]
    [Tooltip("Cantidad maxima de arcos sobre la esfera. Se crean al iniciar y su cantidad visible aumenta con la energia.")]
    [SerializeField] private int surfaceArcCount = 6;

    [Min(0.01f)]
    [Tooltip("Intervalo en segundos entre cambios aleatorios de los arcos. Tambien regula los cambios de forma de los rayos entrantes; valores menores producen cambios mas rapidos.")]
    [SerializeField] private float surfaceRefreshInterval = 0.055f;

    [Min(0.001f)]
    [Tooltip("Separacion de los arcos respecto de la superficie de la esfera, en unidades de mundo. Ayuda a mantener las lineas fuera del nucleo.")]
    [SerializeField] private float surfaceOffset = 0.02f;

    [Tooltip("Multiplicador del brillo del prefab durante la carga: X con energia 0 e Y con energia 1. Afecta los arcos y los rayos entrantes.")]
    [SerializeField] private Vector2 lightningIntensity = new Vector2(0.25f, 2f);

    [Tooltip("Multiplicador del grosor principal del prefab durante la carga: X con energia 0 e Y con energia 1. Se aplica al Glow y al Core de los arcos y rayos entrantes.")]
    [SerializeField] private Vector2 lightningWidth = new Vector2(0.15f, 0.6f);

    [Header("Rayos In")]
    [Range(1, 16)]
    [Tooltip("Cantidad maxima de rayos entrantes simultaneos. Las instancias se crean durante Awake y se reutilizan.")]
    [SerializeField] private int inwardRayCount = 6;

    [Tooltip("Distancia adicional desde la superficie de la esfera, en metros mundiales.")]
    [Min(0.05f)]
    [SerializeField] private float intakeDistance = 2f;

    [Tooltip("Ritmo de aparicion de rayos entrantes por segundo: X con energia 0 e Y con energia 1, limitado por las instancias disponibles.")]
    [SerializeField] private Vector2 inwardRaysPerSecond = new Vector2(2f, 20f);

    [Tooltip("Duracion del recorrido de cada rayo entrante, en segundos: X con energia 0 e Y con energia 1. Valores menores hacen que llegue mas rapido.")]
    [SerializeField] private Vector2 inwardTravelDuration = new Vector2(0.5f, 0.16f);

    [Min(0f)]
    [Tooltip("Desviacion lateral de los rayos entrantes, en unidades de mundo. Valores mayores producen un trazado mas irregular; la desviacion disminuye en los extremos del recorrido.")]
    [SerializeField] private float inwardRoughness = 0.12f;

    [Header("Particles In")]
    [Tooltip("Cantidad de particulas emitidas por segundo: X con energia 0 e Y con energia 1.")]
    [SerializeField] private Vector2 particlesPerSecond = new Vector2(12f, 160f);

    [Tooltip("Velocidad de las particulas hacia el centro, en unidades de mundo por segundo: X con energia 0 e Y con energia 1.")]
    [SerializeField] private Vector2 particleSpeed = new Vector2(1f, 7f);

    [Tooltip("Tamano inicial de las particulas al emitirlas: X con energia 0 e Y con energia 1.")]
    [SerializeField] private Vector2 particleSize = new Vector2(0.025f, 0.07f);

    [ColorUsage(true, true)]
    [Tooltip("Color asignado a las particulas al emitirlas. El resultado visible tambien depende del material del sistema de particulas.")]
    [SerializeField] private Color particleColor = new Color(0.3f, 0.8f, 1f, 1f);

    [Range(32, 4096)]
    [Tooltip("Cantidad maxima de particulas y tamano del buffer reutilizado para actualizarlas. Se configura al iniciar.")]
    [SerializeField] private int maxParticles = 512;

    [Header("Eventos")]
    [Tooltip("Evento ejecutado al comenzar una carga, despues de activar y actualizar sus efectos visuales.")]
    [SerializeField] private UnityEvent onChargeStarted = new UnityEvent();

    [Tooltip("Evento ejecutado al completar la carga y entrar en Ready, antes del disparo automatico si corresponde.")]
    [SerializeField] private UnityEvent onFullyCharged = new UnityEvent();

    [Tooltip("Evento del disparo real, despues de que terminan las particulas y rayos entrantes. El laser asignado ya se reproduce desde Shoot.")]
    [SerializeField] private UnityEvent onFire = new UnityEvent();

    [Tooltip("Evento ejecutado cuando se cancela una carga activa o lista. No se ejecuta si ya estaba en Idle.")]
    [SerializeField] private UnityEvent onChargeCancelled = new UnityEvent();

    public ChargeState State { get; private set; }
    public float Charge01 { get; private set; }
    public bool IsReady => State == ChargeState.Ready;

    [Header("Disparo del laser")]
    [Tooltip("Instancia de ProceduralLightning usada para el disparo final. Shoot llama a Play unicamente si su raycast encuentra un impacto.")]
    [SerializeField] private ProceduralLightning lightning;

    [Tooltip("Objetivo del disparo final. Fire pasa este Transform a Shoot para calcular la direccion desde Origin.")]
    [SerializeField] private Transform target;

    [Tooltip("Transform desde cuya posicion sale el raycast y el rayo del disparo final.")]
    [SerializeField] private Transform origin;

    [Tooltip("Distancia maxima del raycast del disparo final, en unidades de mundo.")]
    [SerializeField] private float _rayDistance;

    [Tooltip("Capas que puede detectar el raycast del disparo final. Los colliders trigger se ignoran.")]
    [SerializeField] private LayerMask _hitLayer;

    [Header("Retroceso IK")]
    [Tooltip("Target de la restriccion IK del laser. Se mueve al disparar y vuelve a su posicion local inicial. Ningun otro controlador debe escribir su posicion durante el retroceso.")]
    [SerializeField] private Transform recoilTarget;

    [Min(0f)]
    [Tooltip("Distancia de retroceso en unidades de mundo cuando la curva vale 1. La direccion es opuesta al disparo.")]
    [SerializeField] private float recoilDistance = 0.25f;

    [Min(0.01f)]
    [Tooltip("Duracion total del retroceso y regreso, en segundos.")]
    [SerializeField] private float recoilDuration = 0.4f;

    [Tooltip("X: tiempo normalizado de 0 a 1. Y: desplazamiento, donde 0 es reposo y 1 es Recoil Distance. Usa inicio (0,0), pico (0.2,1) y final (1,0). Al terminar siempre se restaura la posicion inicial.")]
    [SerializeField]
    private AnimationCurve recoilCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.2f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 0f)
    );

    [Tooltip("Target capturado para el retroceso actual. Permite restaurarlo aunque cambie la referencia del Inspector.")]
    private Transform activeRecoilTarget;

    [Tooltip("Posicion local del Target antes del retroceso actual.")]
    private Vector3 recoilStartLocalPosition;

    [Tooltip("Desplazamiento maximo opuesto al disparo, convertido al espacio local del padre del Target.")]
    private Vector3 recoilLocalOffset;

    [Tooltip("Tiempo transcurrido del retroceso actual, en segundos.")]
    private float recoilElapsed;

    [Tooltip("Duracion capturada al iniciar el retroceso actual, en segundos.")]
    private float activeRecoilDuration;

    [Tooltip("Indica si se esta aplicando el retroceso al Target IK.")]
    private bool recoilActive;

    private sealed class Arc
    {
        [Tooltip("Instancia reutilizable de ProceduralLightning que dibuja este arco o rayo entrante.")]
        public ProceduralLightning visual;

        [Tooltip("Direccion radial del arco o rayo en el espacio de orientacion de la esfera; se rota al mundo al dibujarlo.")]
        public Vector3 direction;

        [Tooltip("Direccion tangente que determina el plano y sentido del arco sobre la esfera.")]
        public Vector3 tangent;

        [Tooltip("Extension angular del arco sobre la esfera, en radianes. Se elige aleatoriamente al renovar el arco.")]
        public float angle;

        [Tooltip("Semilla interna del ruido utilizado para generar la forma de este arco o rayo.")]
        public int seed;
    }

    private sealed class Incoming
    {
        [Tooltip("Instancia reutilizable de ProceduralLightning que dibuja este arco o rayo entrante.")]
        public ProceduralLightning visual;

        [Tooltip("Direccion radial del arco o rayo en el espacio de orientacion de la esfera; se rota al mundo al dibujarlo.")]
        public Vector3 direction;

        [Tooltip("Tiempo transcurrido desde que se activo este rayo entrante, en segundos.")]
        public float age;

        [Tooltip("Duracion del recorrido actual del rayo entrante, en segundos.")]
        public float duration;

        [Tooltip("Semilla interna del ruido utilizado para generar la forma de este arco o rayo.")]
        public int seed;

        [Tooltip("Estado interno: indica si este rayo entrante esta recorriendo su trayectoria.")]
        public bool active;
    }

    [Tooltip("Instancias y datos internos de los arcos que se dibujan sobre la esfera.")]
    private Arc[] arcs;

    [Tooltip("Instancias y datos internos de los rayos que viajan hacia la esfera.")]
    private Incoming[] rays;

    [Tooltip("Objeto contenedor creado durante Awake para las instancias visuales de carga; se destruye junto con el controlador.")]
    private Transform visualRoot;

    [Tooltip("Buffer reutilizado para leer y actualizar las particulas sin crear un array nuevo cada frame.")]
    private ParticleSystem.Particle[] particleBuffer;

    [Tooltip("Bloque de propiedades usado para cambiar el brillo del nucleo sin modificar el material compartido.")]
    private MaterialPropertyBlock coreProperties;

    [Tooltip("Identificador calculado de la propiedad del shader que controla la intensidad del nucleo.")]
    private int coreIntensityId;

    [Tooltip("Estado interno: indica que las referencias y los recursos ya fueron inicializados.")]
    private bool initialized;

    [Tooltip("Estado interno: indica si se debe disparar automaticamente al completar la carga.")]
    private bool autoFire;

    [Tooltip("Tiempo transcurrido de la carga actual, en segundos.")]
    private float elapsed;

    [Tooltip("Cuenta regresiva hasta renovar las direcciones, tangentes y semillas de los arcos de carga.")]
    private float surfaceTimer;

    [Tooltip("Acumulador de emision de rayos entrantes. Cada unidad permite activar una instancia disponible.")]
    private float rayBudget;

    [Tooltip("Acumulador de emision de particulas. Conserva las fracciones entre frames para mantener el ritmo configurado.")]
    private float particleBudget;

    private void Awake()
    {
        if (!energySphere || !energyCore || !coreRenderer || !lightningPrefab)
        {
            Debug.LogError("LaserChargeController: faltan referencias de la esfera, nucleo, renderer o prefab de rayos.", this);
            enabled = false;
            return;
        }

        if (energyCore == energySphere.transform || energySphere.transform.IsChildOf(energyCore))
        {
            Debug.LogError("LaserChargeController: el collider debe estar separado del nucleo y no puede ser hijo de el.", this);
            enabled = false;
            return;
        }

        coreProperties = new MaterialPropertyBlock();
        coreIntensityId = Shader.PropertyToID(coreIntensityProperty);

        if (coreRenderer.sharedMaterial && !coreRenderer.sharedMaterial.HasProperty(coreIntensityId))
            Debug.LogWarning("El material del nucleo no tiene " + coreIntensityProperty + ". El tamano cambiara, pero no su brillo.", this);

        energySphere.isTrigger = true;

        visualRoot = new GameObject("Charge Lightning Instances").transform;
        visualRoot.SetParent(transform, false);

        arcs = new Arc[surfaceArcCount];

        for (int i = 0; i < arcs.Length; i++)
        {
            arcs[i] = new Arc { visual = CreateVisual() };
            RandomizeArc(arcs[i]);
        }

        rays = new Incoming[inwardRayCount];

        for (int i = 0; i < rays.Length; i++)
            rays[i] = new Incoming { visual = CreateVisual() };

        if (particlesIn)
            ConfigureParticles();

        initialized = true;
        HideVisuals();
    }

    private void ConfigureParticles()
    {
        particlesIn.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particlesIn.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Custom;
        main.customSimulationSpace = energySphere.transform;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.maxParticles = maxParticles;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        var emission = particlesIn.emission;
        emission.enabled = false;

        var shape = particlesIn.shape;
        shape.enabled = false;

        var velocity = particlesIn.velocityOverLifetime;
        velocity.enabled = false;

        var force = particlesIn.forceOverLifetime;
        force.enabled = false;

        var noise = particlesIn.noise;
        noise.enabled = false;

        var collision = particlesIn.collision;
        collision.enabled = false;

        var inherit = particlesIn.inheritVelocity;
        inherit.enabled = false;

        var limit = particlesIn.limitVelocityOverLifetime;
        limit.enabled = false;

        var external = particlesIn.externalForces;
        external.enabled = false;

        var triggers = particlesIn.trigger;
        triggers.enabled = false;

        var sub = particlesIn.subEmitters;
        sub.enabled = false;

        particleBuffer = new ParticleSystem.Particle[maxParticles];
    }

    private ProceduralLightning CreateVisual()
    {
        ProceduralLightning copy = Instantiate(lightningPrefab, visualRoot);
        copy.gameObject.SetActive(true);
        copy.PrepareChargeVisual();

        return copy;
    }

    [ContextMenu("Play / Charge And Fire")]
    public void ChargeAndFire()
    {
        Begin(true);
    }

    [ContextMenu("Play / Begin Charge (manual fire)")]
    public void BeginCharge()
    {
        Begin(false);
    }

    private void Begin(bool fireAutomatically)
    {
        if (!initialized || !isActiveAndEnabled)
            return;

        HideVisuals();

        autoFire = fireAutomatically;
        elapsed = 0f;
        Charge01 = 0f;
        surfaceTimer = 0f;
        rayBudget = 0f;
        particleBudget = 0f;

        State = ChargeState.Charging;

        energySphere.enabled = true;
        coreRenderer.enabled = true;

        if (particlesIn)
            particlesIn.Play(false);

        UpdateVisuals(0f);
        onChargeStarted.Invoke();
    }

    public void Shoot(Transform target)
    {
        Vector3 start = origin.position;
        Vector3 direction = target.position - start;
        Vector3 end = start + direction * _rayDistance;

        if (Physics.Raycast(start, direction, out RaycastHit hit, _rayDistance, _hitLayer, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            lightning.Play(origin.position, end);


            // >>> NUEVO BLOQUE: resolver IStasis desde proxy o desde padres <<<
            IStasis stasisComponent = null;
            GameObject objStaseable = null;

            // 1) Si le pegamos a un proxy registrado
            if (StasisRegistry.TryGet(hit.collider, out var stasisFromProxy))
            {
                stasisComponent = stasisFromProxy;
                objStaseable = ((MonoBehaviour)stasisComponent).gameObject;
            }
            else
            {
                // 2) Buscar en la jerarquía del objeto impactado (plataforma hija - engrane padre)
                var stasisInParents = hit.collider.GetComponentInParent<IStasis>();
                if (stasisInParents != null)
                {
                    stasisComponent = stasisInParents;
                    objStaseable = ((MonoBehaviour)stasisComponent).gameObject;
                }
            }

            if (stasisComponent != null)
            {
                // 3) Mantener tu lógica de StasisRoot (si existe, preferir el IStasis dentro del root)
                var root = objStaseable.GetComponentInParent<StasisRoot>();
                if (root)
                {
                    var found = root.GetComponentsInChildren<MonoBehaviour>()
                                    .OfType<IStasis>()
                                    .FirstOrDefault();
                    if (found != null)
                    {
                        stasisComponent = found;
                        objStaseable = ((MonoBehaviour)stasisComponent).gameObject;
                    }
                }


                stasisComponent.StatisEffectActivate();
            }


        }

        BeginRecoil(direction);

        //target.position = end;

        //lightning.SetEndpoints(origin, target);
    }

    private void BeginRecoil(Vector3 shotDirection)
    {
        if (!isActiveAndEnabled)
            return;

        ResetRecoil();

        if (!recoilTarget || recoilDistance <= 0f)
            return;

        if (shotDirection.sqrMagnitude < 0.000001f)
            return;

        activeRecoilTarget = recoilTarget;
        recoilStartLocalPosition = activeRecoilTarget.localPosition;

        Vector3 worldOffset = -shotDirection.normalized * recoilDistance;
        Transform parent = activeRecoilTarget.parent;

        if (parent)
            recoilLocalOffset = parent.InverseTransformVector(worldOffset);
        else
            recoilLocalOffset = worldOffset;

        recoilElapsed = 0f;
        activeRecoilDuration = Mathf.Max(0.01f, recoilDuration);
        recoilActive = true;
        ApplyRecoil(0f);
    }

    private void Update()
    {
        if (!recoilActive)
            return;

        if (!activeRecoilTarget)
        {
            ResetRecoil();
            return;
        }

        recoilElapsed += Time.deltaTime;

        if (recoilElapsed >= activeRecoilDuration)
        {
            ResetRecoil();
            return;
        }

        float progress = Mathf.Clamp01(recoilElapsed / activeRecoilDuration);
        ApplyRecoil(progress);
    }

    private void ApplyRecoil(float progress)
    {
        float amount;

        if (recoilCurve != null && recoilCurve.length > 0)
        {
            amount = recoilCurve.Evaluate(progress);
        }
        else if (progress <= 0.2f)
        {
            amount = Mathf.SmoothStep(0f, 1f, progress / 0.2f);
        }
        else
        {
            amount = Mathf.SmoothStep(1f, 0f, (progress - 0.2f) / 0.8f);
        }

        activeRecoilTarget.localPosition = recoilStartLocalPosition + recoilLocalOffset * amount;
    }

    private void ResetRecoil()
    {
        if (recoilActive && activeRecoilTarget)
            activeRecoilTarget.localPosition = recoilStartLocalPosition;

        recoilActive = false;
        activeRecoilTarget = null;
        recoilElapsed = 0f;
    }

    private void LateUpdate()
    {
        if (!initialized || State == ChargeState.Idle)
            return;

        float dt = Time.deltaTime;

        if (State == ChargeState.Charging)
        {
            elapsed += dt;
            Charge01 = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, chargeDuration));
        }

        UpdateVisuals(dt);

        if (State == ChargeState.Charging && Charge01 >= 1f)
        {
            State = ChargeState.Ready;
            onFullyCharged.Invoke();

            if (autoFire && State == ChargeState.Ready)
                Fire();
        }

        TryCompleteFire();
    }

    [ContextMenu("Play / Fire")]
    public void Fire()
    {
        if (!initialized || !isActiveAndEnabled || State != ChargeState.Ready)
            return;

        // Deja de emitir, pero mantiene la esfera y actualiza lo que ya esta viajando.
        State = ChargeState.FinishingIntake;
        rayBudget = 0f;
        particleBudget = 0f;

        TryCompleteFire();
    }

    private void TryCompleteFire()
    {
        if (State != ChargeState.FinishingIntake)
            return;

        foreach (Incoming ray in rays)
        {
            if (ray.active)
                return;
        }

        if (particlesIn && particlesIn.particleCount > 0)
            return;

        // Los efectos entrantes terminaron. Ahora se dispara y comienza el retroceso.
        State = ChargeState.Idle;
        Charge01 = 0f;

        HideVisuals();
        Shoot(target);
        onFire.Invoke();
    }

    [ContextMenu("Play / Cancel Charge")]
    public void CancelCharge()
    {
        if (!initialized)
            return;

        bool wasActive = State != ChargeState.Idle;

        State = ChargeState.Idle;
        Charge01 = 0f;

        HideVisuals();

        if (wasActive)
            onChargeCancelled.Invoke();
    }

    private void UpdateVisuals(float dt)
    {
        float energy = Mathf.Clamp01(energyCurve == null ? Charge01 : energyCurve.Evaluate(Charge01));
        float pulseWeight = Mathf.InverseLerp(0.75f, 1f, Charge01);
        float pulse = 1f + Mathf.Sin(Time.time * finalPulseFrequency * Mathf.PI * 2f) * finalPulseAmount * pulseWeight;

        energySphere.radius = Mathf.Lerp(minimumRadius, maximumRadius, energy) * pulse;

        Vector3 center = energySphere.transform.TransformPoint(energySphere.center);
        float radius = energySphere.radius * MaxScale(energySphere.transform.lossyScale);
        float parentScale = energyCore.parent ? MaxScale(energyCore.parent.lossyScale) : 1f;

        energyCore.position = center;
        energyCore.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.0001f, parentScale));

        coreRenderer.GetPropertyBlock(coreProperties);
        coreProperties.SetFloat(coreIntensityId, Mathf.Lerp(coreIntensity.x, coreIntensity.y, energy) * pulse);
        coreRenderer.SetPropertyBlock(coreProperties);

        if (chargeLight)
        {
            chargeLight.enabled = true;
            chargeLight.transform.position = center;
            chargeLight.range = lightRange;
            chargeLight.intensity = Mathf.Lerp(lightIntensity.x, lightIntensity.y, energy) * pulse;
        }

        float intensity = Mathf.Lerp(lightningIntensity.x, lightningIntensity.y, energy) * pulse;
        float width = Mathf.Lerp(lightningWidth.x, lightningWidth.y, energy);

        surfaceTimer -= dt;
        bool refresh = surfaceTimer <= 0f;

        if (refresh)
            surfaceTimer = Mathf.Max(0.01f, surfaceRefreshInterval);

        int visibleArcs = Mathf.Clamp(Mathf.CeilToInt(Mathf.Lerp(1, arcs.Length, energy)), 1, arcs.Length);
        Quaternion rotation = energySphere.transform.rotation;

        for (int i = 0; i < arcs.Length; i++)
        {
            Arc arc = arcs[i];

            if (i >= visibleArcs)
            {
                arc.visual.HideChargeVisual();
                continue;
            }

            if (refresh)
                RandomizeArc(arc);

            Vector3 direction = rotation * arc.direction;
            Vector3 tangent = rotation * arc.tangent;

            arc.visual.DrawChargeSphere(center, radius, direction, tangent, arc.angle, surfaceOffset, intensity, width, arc.seed);
        }

        UpdateIncoming(dt, energy, center, radius, intensity, width);
        UpdateParticles(dt, energy, center, radius);
    }

    private static void RandomizeArc(Arc arc)
    {
        arc.direction = Random.onUnitSphere;

        Vector3 reference = Mathf.Abs(arc.direction.y) > 0.95f ? Vector3.right : Vector3.up;

        arc.tangent = Vector3.Cross(arc.direction, reference).normalized;
        arc.tangent = Quaternion.AngleAxis(Random.Range(0f, 360f), arc.direction) * arc.tangent;
        arc.angle = Random.Range(65f, 155f) * Mathf.Deg2Rad;
        arc.seed = Random.Range(0, 10000);
    }

    private void UpdateIncoming(float dt, float energy, Vector3 center, float radius, float intensity, float width)
    {
        bool allowEmission = State != ChargeState.FinishingIntake;

        if (allowEmission)
        {
            float spawnRate = Mathf.Max(0f, Mathf.Lerp(inwardRaysPerSecond.x, inwardRaysPerSecond.y, energy));
            rayBudget = Mathf.Min(rays.Length, rayBudget + dt * spawnRate);
        }

        foreach (Incoming ray in rays)
        {
            if (allowEmission && !ray.active && rayBudget >= 1f)
            {
                rayBudget -= 1f;

                ray.active = true;
                ray.age = 0f;
                ray.duration = Mathf.Max(0.02f, Mathf.Lerp(inwardTravelDuration.x, inwardTravelDuration.y, energy));
                ray.direction = Random.onUnitSphere;
                ray.seed = Random.Range(0, 10000);
            }

            if (!ray.active)
                continue;

            ray.age += dt;
            float t = Mathf.Clamp01(ray.age / ray.duration);

            if (t >= 1f)
            {
                ray.active = false;
                ray.visual.HideChargeVisual();
                continue;
            }

            Vector3 direction = energySphere.transform.rotation * ray.direction;

            float envelope = Mathf.Sin(t * Mathf.PI);
            float headDistance = radius + intakeDistance * (1f - t);
            float tailDistance = headDistance + intakeDistance * 0.4f * envelope;
            int geometrySeed = ray.seed + Mathf.FloorToInt(ray.age / Mathf.Max(0.01f, surfaceRefreshInterval));

            Vector3 tail = center + direction * tailDistance;
            Vector3 head = center + direction * headDistance;

            ray.visual.DrawChargeInward(tail, head, intensity, width, inwardRoughness * envelope, geometrySeed);
        }
    }

    private void UpdateParticles(float dt, float energy, Vector3 center, float radius)
    {
        if (!particlesIn)
            return;

        Transform space = energySphere.transform;

        int spawn = 0;

        if (State != ChargeState.FinishingIntake)
        {
            float spawnRate = Mathf.Max(0f, Mathf.Lerp(particlesPerSecond.x, particlesPerSecond.y, energy));
            particleBudget = Mathf.Min(maxParticles, particleBudget + dt * spawnRate);
            spawn = Mathf.FloorToInt(particleBudget);
            particleBudget -= spawn;
        }

        for (int i = 0; i < spawn; i++)
        {
            Vector3 position = center + Random.onUnitSphere * (radius + intakeDistance);

            var emission = new ParticleSystem.EmitParams
            {
                position = space.InverseTransformPoint(position),
                velocity = Vector3.zero,
                startLifetime = 10f,
                startSize = Mathf.Lerp(particleSize.x, particleSize.y, energy),
                startColor = particleColor
            };

            particlesIn.Emit(emission, 1);
        }

        int count = particlesIn.GetParticles(particleBuffer);
        float speed = Mathf.Max(0.01f, Mathf.Lerp(particleSpeed.x, particleSpeed.y, energy));

        for (int i = 0; i < count; i++)
        {
            var particle = particleBuffer[i];

            Vector3 world = space.TransformPoint(particle.position);
            Vector3 next = Vector3.MoveTowards(world, center, speed * dt);

            if ((next - center).sqrMagnitude <= radius * radius)
                particle.remainingLifetime = -1f;
            else
                particle.position = space.InverseTransformPoint(next);

            particle.velocity = Vector3.zero;
            particleBuffer[i] = particle;
        }

        particlesIn.SetParticles(particleBuffer, count);
    }

    private void HideVisuals()
    {
        coreRenderer.enabled = false;
        energySphere.enabled = false;

        if (chargeLight)
            chargeLight.enabled = false;

        if (particlesIn)
            particlesIn.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        foreach (Arc arc in arcs)
            arc.visual.HideChargeVisual();

        foreach (Incoming ray in rays)
        {
            ray.active = false;
            ray.visual.HideChargeVisual();
        }
    }

    private static float MaxScale(Vector3 scale)
    {
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }

    private void OnDisable()
    {
        ResetRecoil();
        CancelCharge();
    }

    private void OnDestroy()
    {
        if (visualRoot)
            Destroy(visualRoot.gameObject);
    }

    private void OnValidate()
    {
        chargeDuration = Mathf.Max(0.05f, chargeDuration);
        minimumRadius = Mathf.Max(0.01f, minimumRadius);
        maximumRadius = Mathf.Max(minimumRadius, maximumRadius);
        surfaceArcCount = Mathf.Clamp(surfaceArcCount, 1, 16);
        inwardRayCount = Mathf.Clamp(inwardRayCount, 1, 16);
        intakeDistance = Mathf.Max(0.05f, intakeDistance);
        recoilDistance = Mathf.Max(0f, recoilDistance);
        recoilDuration = Mathf.Max(0.01f, recoilDuration);
    }
}