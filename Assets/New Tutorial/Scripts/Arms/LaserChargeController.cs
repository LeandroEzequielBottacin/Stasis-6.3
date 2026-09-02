using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class LaserChargeController : MonoBehaviour
{
    public enum ChargeState
    {
        Idle,
        Charging,
        Ready
    }

    [Header("Referencias")]
    [SerializeField] private SphereCollider energySphere;

    [Tooltip("Esfera visual sin collider, separada del objeto Energy Sphere.")]
    [SerializeField] private Transform energyCore;

    [SerializeField] private Renderer coreRenderer;

    [Tooltip("Prefab de ProceduralLightning con referencias internas a sus renderers.")]
    [SerializeField] private ProceduralLightning lightningPrefab;

    [SerializeField] private ParticleSystem particlesIn;
    [SerializeField] private Light chargeLight;

    [Header("Tiempo y crecimiento")]
    [Min(0.05f)]
    [SerializeField] private float chargeDuration = 2.5f;

    [SerializeField] private AnimationCurve energyCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Radio local inicial del SphereCollider. Usar escala uniforme en la jerarquía.")]
    [Min(0.01f)]
    [SerializeField] private float minimumRadius = 0.08f;

    [Min(0.01f)]
    [SerializeField] private float maximumRadius = 0.55f;

    [Range(0f, 0.15f)]
    [SerializeField] private float finalPulseAmount = 0.05f;

    [Min(0f)]
    [SerializeField] private float finalPulseFrequency = 12f;

    [Header("Núcleo luminoso")]
    [Tooltip("Nombre de la propiedad Float del shader del núcleo.")]
    [SerializeField] private string coreIntensityProperty = "_Intensity";

    [SerializeField] private Vector2 coreIntensity = new Vector2(0.5f, 15f);
    [SerializeField] private Vector2 lightIntensity = new Vector2(0f, 8f);

    [Min(0.01f)]
    [SerializeField] private float lightRange = 5f;

    [Header("Rayos sobre la esfera")]
    [Range(1, 16)]
    [SerializeField] private int surfaceArcCount = 6;

    [Min(0.01f)]
    [SerializeField] private float surfaceRefreshInterval = 0.055f;

    [Min(0.001f)]
    [SerializeField] private float surfaceOffset = 0.02f;

    [SerializeField] private Vector2 lightningIntensity = new Vector2(0.25f, 2f);
    [SerializeField] private Vector2 lightningWidth = new Vector2(0.15f, 0.6f);

    [Header("Rayos In")]
    [Range(1, 16)]
    [SerializeField] private int inwardRayCount = 6;

    [Tooltip("Distancia adicional desde la superficie de la esfera, en metros mundiales.")]
    [Min(0.05f)]
    [SerializeField] private float intakeDistance = 2f;

    [SerializeField] private Vector2 inwardRaysPerSecond = new Vector2(2f, 20f);
    [SerializeField] private Vector2 inwardTravelDuration = new Vector2(0.5f, 0.16f);

    [Min(0f)]
    [SerializeField] private float inwardRoughness = 0.12f;

    [Header("Particles In")]
    [SerializeField] private Vector2 particlesPerSecond = new Vector2(12f, 160f);
    [SerializeField] private Vector2 particleSpeed = new Vector2(1f, 7f);
    [SerializeField] private Vector2 particleSize = new Vector2(0.025f, 0.07f);

    [ColorUsage(true, true)]
    [SerializeField] private Color particleColor = new Color(0.3f, 0.8f, 1f, 1f);

    [Range(32, 4096)]
    [SerializeField] private int maxParticles = 512;

    [Header("Eventos")]
    [SerializeField] private UnityEvent onChargeStarted = new UnityEvent();
    [SerializeField] private UnityEvent onFullyCharged = new UnityEvent();

    [Tooltip("Conectar aquí el método Play/Fire de tu láser.")]
    [SerializeField] private UnityEvent onFire = new UnityEvent();

    [SerializeField] private UnityEvent onChargeCancelled = new UnityEvent();

    public ChargeState State { get; private set; }
    public float Charge01 { get; private set; }
    public bool IsReady => State == ChargeState.Ready;



    [Header("Disparo del láser")]
    [SerializeField] private ProceduralLightning lightning;
    [SerializeField] private Transform target;
    [SerializeField] private Transform origin;
    [SerializeField] private float _rayDistance;
    [SerializeField] private LayerMask _hitLayer;
    private sealed class Arc
    {
        public ProceduralLightning visual;
        public Vector3 direction;
        public Vector3 tangent;
        public float angle;
        public int seed;
    }

    private sealed class Incoming
    {
        public ProceduralLightning visual;
        public Vector3 direction;
        public float age;
        public float duration;
        public int seed;
        public bool active;
    }

    private Arc[] arcs;
    private Incoming[] rays;

    private Transform visualRoot;
    private ParticleSystem.Particle[] particleBuffer;
    private MaterialPropertyBlock coreProperties;

    private int coreIntensityId;

    private bool initialized;
    private bool autoFire;

    private float elapsed;
    private float surfaceTimer;
    private float rayBudget;
    private float particleBudget;

    private void Awake()
    {
        if (!energySphere || !energyCore || !coreRenderer || !lightningPrefab)
        {
            Debug.LogError("LaserChargeController: faltan referencias de la esfera, núcleo, renderer o prefab de rayos.", this);
            enabled = false;
            return;
        }

        if (energyCore == energySphere.transform || energySphere.transform.IsChildOf(energyCore))
        {
            Debug.LogError("LaserChargeController: el collider debe estar separado del núcleo y no puede ser hijo de él.", this);
            enabled = false;
            return;
        }

        coreProperties = new MaterialPropertyBlock();
        coreIntensityId = Shader.PropertyToID(coreIntensityProperty);

        if (coreRenderer.sharedMaterial && !coreRenderer.sharedMaterial.HasProperty(coreIntensityId))
            Debug.LogWarning("El material del núcleo no tiene " + coreIntensityProperty + ". El tamaño cambiará, pero no su brillo.", this);

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
        }

        //target.position = end;

        //lightning.SetEndpoints(origin, target);
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
    }

    [ContextMenu("Play / Fire")]
    public void Fire()
    {
        if (!initialized || State != ChargeState.Ready)
            return;

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
        float spawnRate = Mathf.Max(0f, Mathf.Lerp(inwardRaysPerSecond.x, inwardRaysPerSecond.y, energy));
        rayBudget = Mathf.Min(rays.Length, rayBudget + dt * spawnRate);

        foreach (Incoming ray in rays)
        {
            if (!ray.active && rayBudget >= 1f)
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

        float spawnRate = Mathf.Max(0f, Mathf.Lerp(particlesPerSecond.x, particlesPerSecond.y, energy));
        particleBudget = Mathf.Min(maxParticles, particleBudget + dt * spawnRate);

        int spawn = Mathf.FloorToInt(particleBudget);
        particleBudget -= spawn;

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
    }
}