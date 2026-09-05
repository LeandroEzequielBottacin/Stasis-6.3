using Player.Stasis;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class ProceduralLightning
{
    [Header("Surface Lightning Pool")]
    [Tooltip("Pool que proporciona los rayos sobre las piezas impactadas. Debe asignarse para que comience la propagacion superficial.")]
    [SerializeField] private SurfaceLightningFactoryPool surfaceLightningPool;

    [Min(0f)]
    [Tooltip("Espera en segundos entre la activacion de una pieza y la siguiente durante la propagacion. Con 0 se procesan sin espera.")]
    [SerializeField] private float delayBetweenSurfacePieces = 0.05f;

    [Min(1)]
    [Tooltip("Cantidad de rayos superficiales solicitados por pieza. La cantidad efectiva depende de las instancias disponibles en el pool.")]
    [SerializeField] private int surfaceInstancesPerPiece = 1;

    [Range(0f, 170f)]
    [Tooltip("Angulo de distribucion en grados de los rayos adicionales alrededor de la pieza. Solo interviene cuando hay mas de una instancia por pieza.")]
    [SerializeField] private float surfaceCoverageAngle = 95f;

    [Range(0f, 1f)]
    [Tooltip("Variacion proporcional del angulo de cobertura entre instancias adicionales. Con 0 conserva el angulo configurado.")]
    [SerializeField] private float surfaceCoverageVariation = 0.25f;

    [Tooltip("Muestra en la consola informacion de propagacion y advertencias sobre colliders ausentes o falta de capacidad del pool.")]
    [SerializeField] private bool showSurfaceDebugMessages;

    [Header("Surface Projection")]
    [Min(0f)]
    [Tooltip("Separacion de las lineas respecto del collider en la direccion de su normal, en unidades de mundo.")]
    [SerializeField] private float surfaceLineOffset = 0.015f;

    [Range(0f, 1f)]
    [Tooltip("Multiplicador de Main Displacement para la irregularidad direccional del recorrido principal sobre la superficie.")]
    [SerializeField] private float surfaceMainRoughness = 0.18f;

    [Range(0.01f, 1f)]
    [Tooltip("Factor de reduccion del ruido en cada subdivision del recorrido superficial. Valores proximos a 1 conservan mas irregularidad en los detalles pequenos.")]
    [SerializeField] private float surfaceFractalDecay = 0.56f;

    [Range(0.05f, 1.5f)]
    [Tooltip("Amplitud con la que las ramas se extienden alrededor de la superficie. Valores mayores alejan mas su extremo del punto de nacimiento.")]
    [SerializeField] private float surfaceBranchWrapAmount = 0.55f;

    [Min(0.001f)]
    [Tooltip("Tiempo entre regeneraciones del trazado superficial, en segundos. Menor intervalo implica cambios mas rapidos y mas calculos.")]
    [SerializeField] private float surfaceGeometryRefreshInterval = 0.045f;

    [Range(2, 8)]
    [Tooltip("Cantidad de formas superficiales precalculadas al activar cada instancia. Luego se alternan con Surface Geometry Refresh Interval, conservando un movimiento rapido sin reproyectar contra el collider en cada refresco.")]
    [SerializeField] private int surfaceCachedGeometryCount = 4;

    [Range(0, 8)]
    [Tooltip("Maxima profundidad de subdivision para separar segmentos del collider. Con 0 no refina; valores altos pueden generar mas puntos y trabajo.")]
    [SerializeField] private int surfacePathRefinementDepth = 6;

    [Min(0.0001f)]
    [Tooltip("Separacion minima evaluada en el punto medio de cada segmento, en unidades de mundo. Si es menor, se intenta subdividir y proyectar hasta el limite de refinamiento.")]
    [SerializeField] private float surfaceMinimumClearance = 0.004f;

    [Tooltip("Estado interno que selecciona la actualizacion superficial y evita reproducir el rayo como una instancia normal.")]
    private bool isSurfaceInstance;
    [Tooltip("Collider sobre el cual se proyecta el recorrido de esta instancia superficial.")]
    private Collider instanceSurfaceCollider;
    [Tooltip("Punto mundial de entrada del recorrido superficial actual.")]
    private Vector3 instanceEntryPosition;
    [Tooltip("Punto mundial intermedio que distribuye el recorrido alrededor de la pieza.")]
    private Vector3 instanceWaypointPosition;
    [Tooltip("Punto mundial de salida del recorrido sobre la pieza actual.")]
    private Vector3 instanceExitPosition;
    [Tooltip("Punto mundial en la pieza conectada, anadido como ultimo punto cuando existe conexion directa habilitada.")]
    private Vector3 instanceConnectionPosition;
    [Tooltip("Indica si esta instancia debe anadir un segmento de conexion hacia otra pieza.")]
    private bool instanceIncludesConnection;
    [Tooltip("Tiempo transcurrido de esta instancia superficial, en segundos. Se compara con Burst Duration para terminarla.")]
    private float surfaceInstanceElapsedTime;
    [Tooltip("Cuenta regresiva hasta regenerar el recorrido superficial, en segundos.")]
    private float surfaceGeometryRefreshTimer;
    [Tooltip("Valor interno que cambia la semilla de la geometria al regenerar esta instancia superficial.")]
    private int surfaceGenerationIndex;
    [Tooltip("Semilla aleatoria de la propagacion actual para distribuir las instancias entre las piezas.")]
    private int surfacePropagationSeed;
    [Tooltip("Referencia a la corrutina que activa los rayos de las piezas con el intervalo configurado.")]
    private Coroutine surfacePropagationRoutine;
    [Tooltip("Pool propietario al que vuelve esta instancia superficial al terminar su duracion.")]
    private SurfaceLightningFactoryPool surfacePoolOwner;

    [Tooltip("Formas superficiales persistentes. Sus buffers sobreviven al regreso al pool.")]
    private SurfaceGeometryFrame[] cachedSurfaceGeometries;

    private readonly List<Vector3> firstSurfacePathScratch = new List<Vector3>(64);
    private readonly List<Vector3> secondSurfacePathScratch = new List<Vector3>(64);
    private readonly List<Vector3> rawSurfacePathScratch = new List<Vector3>(64);
    private readonly List<Vector3> branchSurfacePathScratch = new List<Vector3>(32);
    private float[] firstSurfaceOffsets = new float[0];
    private float[] secondSurfaceOffsets = new float[0];
    private readonly List<SurfaceChainPart> surfaceChainBuffer = new List<SurfaceChainPart>(64);
    private readonly Queue<SurfaceChainPart> pendingSurfaceParts = new Queue<SurfaceChainPart>(64);
    private readonly HashSet<StasisElectricVisual> visitedSurfaceVisuals = new HashSet<StasisElectricVisual>();
    private readonly List<StasisElectricVisual> connectedSurfaceVisuals = new List<StasisElectricVisual>(16);
    private readonly List<SurfaceChainPart> surfaceChainPartPool = new List<SurfaceChainPart>(64);
    private int surfaceChainPartPoolIndex;
    private WaitForSeconds cachedSurfacePieceDelay;
    private float cachedSurfacePieceDelaySeconds = -1f;

    [Tooltip("Forma precalculada mostrada actualmente.")]
    private int cachedSurfaceGeometryIndex;

    private void InitializeSurfaceLightning()
    {
        isSurfaceInstance = false;
    }

    private void ValidateSurfaceLightningSettings()
    {
        delayBetweenSurfacePieces = Mathf.Max(0f, delayBetweenSurfacePieces);
        surfaceInstancesPerPiece = Mathf.Max(1, surfaceInstancesPerPiece);
        surfaceCoverageAngle = Mathf.Clamp(surfaceCoverageAngle, 0f, 170f);
        surfaceCoverageVariation = Mathf.Clamp01(surfaceCoverageVariation);
        surfaceLineOffset = Mathf.Max(0f, surfaceLineOffset);
        surfaceGeometryRefreshInterval = Mathf.Max(0.001f, surfaceGeometryRefreshInterval);
        surfaceCachedGeometryCount = Mathf.Clamp(surfaceCachedGeometryCount, 2, 8);
        surfacePathRefinementDepth = Mathf.Clamp(surfacePathRefinementDepth, 0, 8);
        surfaceMinimumClearance = Mathf.Max(0.0001f, surfaceMinimumClearance);
    }

    private void BeginSurfacePropagation(Transform impactedTransform, Vector3 impactPosition, Vector3 impactNormal)
    {
        if (surfaceLightningPool == null)
        {
            if (showSurfaceDebugMessages)
                Debug.LogWarning("Surface Lightning Pool no esta asignado.");

            return;
        }

        if (surfacePropagationRoutine != null)
            StopCoroutine(surfacePropagationRoutine);

        List<SurfaceChainPart> chain = BuildSurfaceChain(impactedTransform);

        if (showSurfaceDebugMessages)
        {
            Debug.Log("Impacto superficial contra: " + impactedTransform.name);
            Debug.Log("Piezas superficiales conectadas: " + chain.Count);
        }

        if (chain.Count == 0)
            return;

        surfacePropagationSeed = Random.Range(1, 1000000);
        surfacePropagationRoutine = StartCoroutine(SpawnSurfaceLightningChain(chain, impactPosition + impactNormal * surfaceLineOffset));
    }

    private List<SurfaceChainPart> BuildSurfaceChain(Transform impactedTransform)
    {
        surfaceChainBuffer.Clear();
        pendingSurfaceParts.Clear();
        visitedSurfaceVisuals.Clear();
        surfaceChainPartPoolIndex = 0;
        Transform firstTransform = GetFirstSurfaceStasisTransform(impactedTransform);

        if (firstTransform == null)
            return surfaceChainBuffer;

        StasisElectricVisual firstVisual = firstTransform.GetComponent<StasisElectricVisual>();

        if (firstVisual == null)
            return surfaceChainBuffer;

        Collider impactedCollider = impactedTransform.GetComponent<Collider>();
        Collider firstCollider = impactedCollider != null ? impactedCollider : firstVisual.GetSurfaceCollider();

        if (firstCollider == null)
            return surfaceChainBuffer;

        SurfaceChainPart firstPart = GetSurfaceChainPart(firstVisual, firstCollider, null, 0);
        pendingSurfaceParts.Enqueue(firstPart);
        visitedSurfaceVisuals.Add(firstVisual);

        while (pendingSurfaceParts.Count > 0)
        {
            SurfaceChainPart currentPart = pendingSurfaceParts.Dequeue();
            surfaceChainBuffer.Add(currentPart);
            GetConnectedStasisVisuals(currentPart.Visual, connectedSurfaceVisuals);

            foreach (StasisElectricVisual connectedVisual in connectedSurfaceVisuals)
            {
                if (connectedVisual == null || visitedSurfaceVisuals.Contains(connectedVisual))
                    continue;

                Collider connectedCollider = connectedVisual.GetSurfaceCollider();

                if (connectedCollider == null)
                {
                    if (showSurfaceDebugMessages)
                        Debug.LogWarning(connectedVisual.name + " no tiene un Collider utilizable para la superficie.");

                    continue;
                }

                visitedSurfaceVisuals.Add(connectedVisual);
                pendingSurfaceParts.Enqueue(GetSurfaceChainPart(connectedVisual, connectedCollider, currentPart, currentPart.Depth + 1));
            }
        }

        return surfaceChainBuffer;
    }

    private void GetConnectedStasisVisuals(StasisElectricVisual currentVisual, List<StasisElectricVisual> connectedVisuals)
    {
        connectedVisuals.Clear();

        if (currentVisual.ConnectionMode == StasisConnectionMode.Arms)
        {
            AddArmParent(currentVisual, connectedVisuals);
            AddArmChildren(currentVisual, connectedVisuals);
        }
        else
        {
            AddFanSiblings(currentVisual, connectedVisuals);
        }

    }

    private SurfaceChainPart GetSurfaceChainPart(StasisElectricVisual visual, Collider surfaceCollider, SurfaceChainPart sourcePart, int depth)
    {
        SurfaceChainPart part;

        if (surfaceChainPartPoolIndex < surfaceChainPartPool.Count)
        {
            part = surfaceChainPartPool[surfaceChainPartPoolIndex];
        }
        else
        {
            part = new SurfaceChainPart();
            surfaceChainPartPool.Add(part);
        }

        surfaceChainPartPoolIndex++;
        part.Set(visual, surfaceCollider, sourcePart, depth);
        return part;
    }

    private static void AddArmParent(StasisElectricVisual currentVisual, List<StasisElectricVisual> connectedVisuals)
    {
        Transform parent = currentVisual.transform.parent;

        if (parent == null)
            return;

        StasisElectricVisual parentVisual = parent.GetComponent<StasisElectricVisual>();

        if (parentVisual != null && parentVisual.ConnectionMode == StasisConnectionMode.Arms)
            connectedVisuals.Add(parentVisual);
    }

    private static void AddArmChildren(StasisElectricVisual currentVisual, List<StasisElectricVisual> connectedVisuals)
    {
        foreach (Transform child in currentVisual.transform)
        {
            StasisElectricVisual childVisual = child.GetComponent<StasisElectricVisual>();

            if (childVisual != null && childVisual.ConnectionMode == StasisConnectionMode.Arms)
                connectedVisuals.Add(childVisual);
        }
    }

    private static void AddFanSiblings(StasisElectricVisual currentVisual, List<StasisElectricVisual> connectedVisuals)
    {
        Transform parent = currentVisual.transform.parent;

        if (parent == null)
            return;

        foreach (Transform sibling in parent)
        {
            if (sibling == currentVisual.transform)
                continue;

            StasisElectricVisual siblingVisual = sibling.GetComponent<StasisElectricVisual>();

            if (siblingVisual != null && siblingVisual.ConnectionMode == StasisConnectionMode.Fan)
                connectedVisuals.Add(siblingVisual);
        }
    }

    private static Transform GetFirstSurfaceStasisTransform(Transform startingTransform)
    {
        Transform currentTransform = startingTransform;

        while (currentTransform != null)
        {
            if (currentTransform.GetComponent(typeof(IStasis)) != null)
                return currentTransform;

            currentTransform = currentTransform.parent;
        }

        return null;
    }

    private IEnumerator SpawnSurfaceLightningChain(List<SurfaceChainPart> chain, Vector3 firstEntryPosition)
    {
        for (int partIndex = 0; partIndex < chain.Count; partIndex++)
        {
            SurfaceChainPart currentPart = chain[partIndex];
            bool isFirstPart = currentPart.SourcePart == null;
            Vector3 entryPosition = isFirstPart ? firstEntryPosition : GetOppositeSurfacePosition(currentPart.SurfaceCollider, ProjectOutsidePointToSurface(currentPart.SurfaceCollider, currentPart.SourcePart.SurfaceCollider.bounds.center));
            Vector3 exitPosition = isFirstPart ? GetOppositeSurfacePosition(currentPart.SurfaceCollider, entryPosition) : ProjectOutsidePointToSurface(currentPart.SurfaceCollider, currentPart.SourcePart.SurfaceCollider.bounds.center);
            Vector3 connectionPosition = isFirstPart ? exitPosition : ProjectOutsidePointToSurface(currentPart.SourcePart.SurfaceCollider, currentPart.SurfaceCollider.bounds.center);
            Vector3 middleSurfacePosition = GetMiddleSurfacePosition(currentPart.SurfaceCollider, entryPosition, exitPosition);
            bool includeConnection = !isFirstPart && IsDirectSurfaceConnectionClear(chain, exitPosition, connectionPosition);

            for (int instanceIndex = 0; instanceIndex < surfaceInstancesPerPiece; instanceIndex++)
            {
                int distributionSeed = surfacePropagationSeed + partIndex * 1009;
                int instanceSeed = distributionSeed + instanceIndex * 137;
                Vector3 distributedWaypoint = GetDistributedSurfacePosition(currentPart.SurfaceCollider, middleSurfacePosition, instanceIndex, surfaceInstancesPerPiece, distributionSeed);
                bool played = surfaceLightningPool.PlaySurfaceLightning(
                    currentPart.SurfaceCollider,
                    entryPosition,
                    distributedWaypoint,
                    exitPosition,
                    connectionPosition,
                    includeConnection,
                    instanceSeed
                );

                if (!played)
                {
                    if (showSurfaceDebugMessages)
                        Debug.LogWarning("El Surface Lightning Pool alcanzo su capacidad maxima.");

                    continue;
                }
            }

            if (delayBetweenSurfacePieces > 0f)
            {
                if (cachedSurfacePieceDelay == null || !Mathf.Approximately(cachedSurfacePieceDelaySeconds, delayBetweenSurfacePieces))
                {
                    cachedSurfacePieceDelaySeconds = delayBetweenSurfacePieces;
                    cachedSurfacePieceDelay = new WaitForSeconds(delayBetweenSurfacePieces);
                }

                yield return cachedSurfacePieceDelay;
            }
        }

        surfacePropagationRoutine = null;
    }

    private Vector3 GetDistributedSurfacePosition(Collider surfaceCollider, Vector3 originalPosition, int instanceIndex, int instanceCount, int angleSeed)
    {
        if (instanceCount <= 1 || instanceIndex == 0 || surfaceCoverageAngle <= 0f)
            return ProjectOutsidePointToSurface(surfaceCollider, originalPosition);

        Vector3 center = surfaceCollider.bounds.center;
        Vector3 radialDirection = GetSurfaceSafeDirection(originalPosition - center, surfaceCollider.transform.up);
        CreatePerpendicularAxes(radialDirection, out Vector3 tangent, out Vector3 binormal);
        int surroundingInstanceCount = Mathf.Max(1, instanceCount - 1);
        float angleOffset = angleSeed * 0.61803398875f;
        float angle = (instanceIndex - 1) / (float)surroundingInstanceCount * Mathf.PI * 2f + angleOffset;
        Vector3 surroundingDirection = tangent * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
        float variation = Mathf.Sin((instanceIndex + angleSeed) * 12.9898f) * surfaceCoverageVariation;
        float coverageAngle = surfaceCoverageAngle * (1f + variation);
        coverageAngle = Mathf.Clamp(coverageAngle, 0f, 170f);
        Vector3 rotationAxis = Vector3.Cross(radialDirection, surroundingDirection).normalized;
        Vector3 distributedDirection = Quaternion.AngleAxis(coverageAngle, rotationAxis) * radialDirection;
        Vector3 outsideTarget = center + distributedDirection * GetSurfaceProjectionDistance(surfaceCollider);
        return ProjectOutsidePointToSurface(surfaceCollider, outsideTarget);
    }

    private Vector3 GetMiddleSurfacePosition(Collider surfaceCollider, Vector3 entryPosition, Vector3 exitPosition)
    {
        Vector3 center = surfaceCollider.bounds.center;
        Vector3 entryDirection = GetSurfaceSafeDirection(entryPosition - center, surfaceCollider.transform.up);
        Vector3 exitDirection = GetSurfaceSafeDirection(exitPosition - center, -entryDirection);
        Vector3 middleDirection = entryDirection + exitDirection;

        if (middleDirection.sqrMagnitude <= 0.0001f)
        {
            CreatePerpendicularAxes(entryDirection, out Vector3 perpendicular, out Vector3 binormal);
            middleDirection = perpendicular;
        }

        Vector3 outsideTarget = center + middleDirection.normalized * GetSurfaceProjectionDistance(surfaceCollider);
        return ProjectOutsidePointToSurface(surfaceCollider, outsideTarget);
    }

    private static bool IsDirectSurfaceConnectionClear(List<SurfaceChainPart> chain, Vector3 startPosition, Vector3 endPosition)
    {
        const int checkCount = 16;

        for (int checkIndex = 1; checkIndex < checkCount; checkIndex++)
        {
            float progress = checkIndex / (float)checkCount;
            Vector3 checkedPosition = Vector3.Lerp(startPosition, endPosition, progress);

            foreach (SurfaceChainPart chainPart in chain)
            {
                Collider checkedCollider = chainPart.SurfaceCollider;

                if (checkedCollider == null)
                    continue;

                if (IsPointInsideSurfaceCollider(checkedPosition, checkedCollider))
                    return false;
            }
        }

        return true;
    }

    private static bool IsPointInsideSurfaceCollider(Vector3 position, Collider surfaceCollider)
    {
        if (surfaceCollider == null || !surfaceCollider.bounds.Contains(position))
            return false;

        Vector3 closestPosition = surfaceCollider.ClosestPoint(position);
        return (closestPosition - position).sqrMagnitude <= 0.00000001f;
    }

    public void PlayOnSurface(Collider surfaceCollider, Vector3 entryPosition, Vector3 waypointPosition, Vector3 exitPosition, Vector3 connectionPosition, bool includeConnection, int generationSeed)
    {
        if (surfaceCollider == null)
            return;

        isSurfaceInstance = true;
        instanceSurfaceCollider = surfaceCollider;
        instanceEntryPosition = entryPosition;
        instanceWaypointPosition = waypointPosition;
        instanceExitPosition = exitPosition;
        instanceConnectionPosition = connectionPosition;
        instanceIncludesConnection = includeConnection;
        surfaceGenerationIndex = generationSeed * 1009;
        surfaceInstanceElapsedTime = 0f;
        surfaceGeometryRefreshTimer = 0f;
        cachedSurfaceGeometryIndex = 0;
        isPlaying = true;
        isMainBoltVisible = true;
        ConfigureSurfaceLineRenderers();
        SetBoltRenderersEnabled(true);
        BuildSurfaceGeometryCache();
        surfaceGeometryRefreshTimer = Mathf.Max(0.001f, surfaceGeometryRefreshInterval);
        ApplyVisualIntensity(1f);
    }

    public void SetSurfacePoolOwner(SurfaceLightningFactoryPool poolOwner)
    {
        surfacePoolOwner = poolOwner;
        isSurfaceInstance = true;
    }

    public void ResetSurfacePoolInstance()
    {
        isPlaying = false;
        isMainBoltVisible = false;
        instanceSurfaceCollider = null;
        instanceEntryPosition = Vector3.zero;
        instanceWaypointPosition = Vector3.zero;
        instanceExitPosition = Vector3.zero;
        instanceConnectionPosition = Vector3.zero;
        instanceIncludesConnection = false;
        surfaceInstanceElapsedTime = 0f;
        surfaceGeometryRefreshTimer = 0f;
        cachedSurfaceGeometryIndex = 0;
        ApplyVisualIntensity(0f);
        SetBoltRenderersEnabled(false);
    }

    private void ConfigureSurfaceLineRenderers()
    {
        ConfigureSurfaceLineRenderer(mainGlowRenderer);
        ConfigureSurfaceLineRenderer(mainCoreRenderer);

        if (branchRenderers == null)
            return;

        foreach (LightningBranchRenderer branch in branchRenderers)
        {
            if (branch == null)
                continue;

            ConfigureSurfaceLineRenderer(branch.glowRenderer);
            ConfigureSurfaceLineRenderer(branch.coreRenderer);
        }
    }

    private static void ConfigureSurfaceLineRenderer(LineRenderer lineRenderer)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
    }

    private void UpdateSurfaceInstance()
    {
        if (!isPlaying || instanceSurfaceCollider == null)
            return;

        surfaceInstanceElapsedTime += Time.deltaTime;

        if (surfaceInstanceElapsedTime >= burstDuration)
        {
            StopSurfaceInstance();
            return;
        }

        surfaceGeometryRefreshTimer -= Time.deltaTime;

        if (surfaceGeometryRefreshTimer <= 0f)
        {
            ShowNextCachedSurfaceGeometry();
            surfaceGeometryRefreshTimer = Mathf.Max(0.001f, surfaceGeometryRefreshInterval);
        }

        float progress = Mathf.Clamp01(surfaceInstanceElapsedTime / burstDuration);
        float intensity = Mathf.Max(0f, burstIntensityCurve.Evaluate(progress));
        ApplyVisualIntensity(intensity);
    }

    private void StopSurfaceInstance()
    {
        if (surfacePoolOwner != null)
        {
            surfacePoolOwner.Release(this);
            return;
        }

        ResetSurfacePoolInstance();
        Destroy(gameObject);
    }

    private void BuildSurfaceGeometryCache()
    {
        int geometryCount = Mathf.Clamp(surfaceCachedGeometryCount, 2, 8);
        int branchCount = branchRenderers == null ? 0 : branchRenderers.Length;
        int originalGenerationIndex = surfaceGenerationIndex;

        EnsureSurfaceGeometryCache(geometryCount, branchCount);

        for (int geometryIndex = 0; geometryIndex < geometryCount; geometryIndex++)
        {
            surfaceGenerationIndex = originalGenerationIndex + geometryIndex;
            GenerateSurfaceGeometryNonAlloc(cachedSurfaceGeometries[geometryIndex]);
        }

        surfaceGenerationIndex = originalGenerationIndex;
        cachedSurfaceGeometryIndex = 0;
        ApplyCachedSurfaceGeometry(cachedSurfaceGeometryIndex);
    }

    private void EnsureSurfaceGeometryCache(int geometryCount, int branchCount)
    {
        if (cachedSurfaceGeometries == null || cachedSurfaceGeometries.Length != geometryCount)
            cachedSurfaceGeometries = new SurfaceGeometryFrame[geometryCount];

        for (int geometryIndex = 0; geometryIndex < geometryCount; geometryIndex++)
        {
            SurfaceGeometryFrame frame = cachedSurfaceGeometries[geometryIndex];

            if (frame == null)
            {
                frame = new SurfaceGeometryFrame();
                cachedSurfaceGeometries[geometryIndex] = frame;
            }

            frame.EnsureBranchCount(branchCount);
        }
    }

    private void ShowNextCachedSurfaceGeometry()
    {
        if (cachedSurfaceGeometries == null || cachedSurfaceGeometries.Length == 0)
            return;

        cachedSurfaceGeometryIndex++;

        if (cachedSurfaceGeometryIndex >= cachedSurfaceGeometries.Length)
            cachedSurfaceGeometryIndex = 0;

        ApplyCachedSurfaceGeometry(cachedSurfaceGeometryIndex);
    }

    private void ApplyCachedSurfaceGeometry(int geometryIndex)
    {
        if (cachedSurfaceGeometries == null)
            return;

        if (geometryIndex < 0 || geometryIndex >= cachedSurfaceGeometries.Length)
            return;

        SurfaceGeometryFrame frame = cachedSurfaceGeometries[geometryIndex];

        if (frame == null || frame.MainCount <= 0)
            return;

        mainPositions = frame.MainPositions;

        if (mainGlowRenderer != null)
        {
            mainGlowRenderer.positionCount = frame.MainCount;
            mainGlowRenderer.SetPositions(frame.MainPositions);
        }

        if (mainCoreRenderer != null)
        {
            mainCoreRenderer.positionCount = frame.MainCount;
            mainCoreRenderer.SetPositions(frame.MainPositions);
        }

        if (branchRenderers == null)
            return;

        for (int branchIndex = 0; branchIndex < branchRenderers.Length; branchIndex++)
        {
            LightningBranchRenderer branch = branchRenderers[branchIndex];

            if (branch == null)
                continue;

            bool visible = branchIndex < frame.BranchVisibility.Length &&
                           frame.BranchVisibility[branchIndex];

            branch.isVisible = visible;
            SetBranchRendererEnabled(branch, visible);

            if (!visible || branchIndex >= frame.BranchPositions.Length)
                continue;

            Vector3[] cachedPositions = frame.BranchPositions[branchIndex];
            int cachedCount = frame.BranchCounts[branchIndex];

            if (cachedPositions == null || cachedCount <= 0)
                continue;

            branch.positions = cachedPositions;

            if (branch.glowRenderer != null)
            {
                branch.glowRenderer.positionCount = cachedCount;
                branch.glowRenderer.SetPositions(cachedPositions);
            }

            if (branch.coreRenderer != null)
            {
                branch.coreRenderer.positionCount = cachedCount;
                branch.coreRenderer.SetPositions(cachedPositions);
            }
        }
    }

    private void GenerateSurfaceGeometryNonAlloc(SurfaceGeometryFrame frame)
    {
        if (instanceSurfaceCollider == null || frame == null)
            return;

        int firstPathSegmentCount = Mathf.Max(2, mainSegmentCount / 2);
        int secondPathSegmentCount = Mathf.Max(2, mainSegmentCount - firstPathSegmentCount);
        GenerateProjectedSurfacePathNonAlloc(instanceSurfaceCollider, instanceEntryPosition, instanceWaypointPosition, firstPathSegmentCount, mainDisplacement * surfaceMainRoughness, surfaceGenerationIndex + noiseSeed, firstSurfacePathScratch);
        GenerateProjectedSurfacePathNonAlloc(instanceSurfaceCollider, instanceWaypointPosition, instanceExitPosition, secondPathSegmentCount, mainDisplacement * surfaceMainRoughness, surfaceGenerationIndex + noiseSeed + 7919, secondSurfacePathScratch);

        if (firstSurfacePathScratch.Count == 0 || secondSurfacePathScratch.Count == 0)
        {
            frame.MainCount = 0;
            return;
        }

        int connectionPointCount = instanceIncludesConnection ? 1 : 0;
        int requiredMainCount = firstSurfacePathScratch.Count + secondSurfacePathScratch.Count - 1 + connectionPointCount;
        EnsureVectorBuffer(ref frame.MainPositions, requiredMainCount);
        int writeIndex = 0;

        for (int index = 0; index < firstSurfacePathScratch.Count; index++)
            frame.MainPositions[writeIndex++] = firstSurfacePathScratch[index];

        for (int index = 1; index < secondSurfacePathScratch.Count; index++)
            frame.MainPositions[writeIndex++] = secondSurfacePathScratch[index];

        if (instanceIncludesConnection)
            frame.MainPositions[writeIndex++] = instanceConnectionPosition;

        frame.MainCount = writeIndex;
        GenerateSurfaceBranchesNonAlloc(frame);
    }

    private void GenerateSurfaceBranchesNonAlloc(SurfaceGeometryFrame frame)
    {
        if (branchRenderers == null || branchRenderers.Length == 0)
            return;

        uint randomState = CreateSurfaceRandomState(surfaceGenerationIndex + noiseSeed * 31);

        for (int branchIndex = 0; branchIndex < branchRenderers.Length; branchIndex++)
        {
            LightningBranchRenderer branch = branchRenderers[branchIndex];

            if (branch == null)
                continue;

            bool visible = NextSurfaceRandom01(ref randomState) <= branchVisibleChance;
            frame.BranchVisibility[branchIndex] = visible;
            frame.BranchCounts[branchIndex] = 0;

            if (!visible)
                continue;

            int lastSurfaceIndex = instanceIncludesConnection ? frame.MainCount - 2 : frame.MainCount - 1;

            if (lastSurfaceIndex < 2)
            {
                frame.BranchVisibility[branchIndex] = false;
                continue;
            }

            int minimumStartIndex = Mathf.Clamp(Mathf.RoundToInt(lastSurfaceIndex * minimumBranchStart), 1, lastSurfaceIndex - 1);
            int maximumStartIndex = Mathf.Clamp(Mathf.RoundToInt(lastSurfaceIndex * maximumBranchStart), minimumStartIndex, lastSurfaceIndex - 1);
            int branchStartIndex = NextSurfaceRandomRange(ref randomState, minimumStartIndex, maximumStartIndex + 1);
            Vector3 branchStartPosition = frame.MainPositions[branchStartIndex];
            Vector3 center = instanceSurfaceCollider.bounds.center;
            Vector3 surfaceDirection = GetSurfaceSafeDirection(branchStartPosition - center, instanceSurfaceCollider.transform.up);
            CreatePerpendicularAxes(surfaceDirection, out Vector3 surfaceTangent, out Vector3 surfaceBinormal);
            float angle = NextSurfaceRandom01(ref randomState) * Mathf.PI * 2f;
            float lengthRatio = Mathf.Lerp(minimumBranchLengthRatio, maximumBranchLengthRatio, NextSurfaceRandom01(ref randomState));
            float wrapAmount = surfaceBranchWrapAmount * Mathf.Lerp(0.65f, 1.35f, lengthRatio / Mathf.Max(maximumBranchLengthRatio, 0.001f));
            Vector3 wrappingDirection = surfaceTangent * Mathf.Cos(angle) + surfaceBinormal * Mathf.Sin(angle);
            Vector3 branchEndDirection = (surfaceDirection + wrappingDirection * wrapAmount).normalized;
            Vector3 branchOutsideTarget = center + branchEndDirection * (instanceSurfaceCollider.bounds.extents.magnitude + 1f);
            Vector3 branchEndPosition = ProjectOutsidePointToSurface(instanceSurfaceCollider, branchOutsideTarget);
            GenerateProjectedSurfacePathNonAlloc(instanceSurfaceCollider, branchStartPosition, branchEndPosition, branchSegmentCount, branchDisplacement, surfaceGenerationIndex + branchIndex * 127 + 7001, branchSurfacePathScratch);

            EnsureVectorBuffer(ref frame.BranchPositions[branchIndex], branchSurfacePathScratch.Count);

            for (int pointIndex = 0; pointIndex < branchSurfacePathScratch.Count; pointIndex++)
            {
                frame.BranchPositions[branchIndex][pointIndex] =
                    ProjectOutsidePointToSurface(instanceSurfaceCollider, branchSurfacePathScratch[pointIndex]);
            }

            frame.BranchCounts[branchIndex] = branchSurfacePathScratch.Count;
        }
    }

    private void GenerateProjectedSurfacePathNonAlloc(Collider surfaceCollider, Vector3 startPosition, Vector3 endPosition, int segmentCount, float displacement, int seed, List<Vector3> result)
    {
        segmentCount = Mathf.Max(2, segmentCount);
        result.Clear();
        rawSurfacePathScratch.Clear();
        EnsureFloatBuffer(ref firstSurfaceOffsets, segmentCount + 1);
        EnsureFloatBuffer(ref secondSurfaceOffsets, segmentCount + 1);
        BuildSurfaceFractalOffsetsNonAlloc(firstSurfaceOffsets, segmentCount, seed);
        BuildSurfaceFractalOffsetsNonAlloc(secondSurfaceOffsets, segmentCount, seed + 479);

        Vector3 center = surfaceCollider.bounds.center;
        Vector3 projectedStart = ProjectOutsidePointToSurface(surfaceCollider, startPosition);
        Vector3 projectedEnd = ProjectOutsidePointToSurface(surfaceCollider, endPosition);
        Vector3 startDirection = GetSurfaceSafeDirection(projectedStart - center, surfaceCollider.transform.up);
        Vector3 endDirection = GetSurfaceSafeDirection(projectedEnd - center, -startDirection);
        float outsideDistance = surfaceCollider.bounds.extents.magnitude + 1f;

        for (int index = 0; index <= segmentCount; index++)
        {
            float progress = index / (float)segmentCount;
            float envelope = Mathf.Sin(progress * Mathf.PI);
            Vector3 radialDirection = Vector3.Slerp(startDirection, endDirection, progress).normalized;
            CreatePerpendicularAxes(radialDirection, out Vector3 tangent, out Vector3 binormal);
            Vector3 irregularDirection = radialDirection + tangent * firstSurfaceOffsets[index] * displacement * envelope + binormal * secondSurfaceOffsets[index] * displacement * envelope;
            irregularDirection.Normalize();
            Vector3 outsidePosition = center + irregularDirection * outsideDistance;
            rawSurfacePathScratch.Add(ProjectOutsidePointToSurface(surfaceCollider, outsidePosition));
        }

        rawSurfacePathScratch[0] = projectedStart;
        rawSurfacePathScratch[rawSurfacePathScratch.Count - 1] = projectedEnd;
        result.Add(rawSurfacePathScratch[0]);

        if (surfacePathRefinementDepth <= 0)
        {
            for (int index = 1; index < rawSurfacePathScratch.Count; index++)
                result.Add(rawSurfacePathScratch[index]);

            return;
        }

        for (int index = 0; index < rawSurfacePathScratch.Count - 1; index++)
            RefineSurfaceSegment(surfaceCollider, rawSurfacePathScratch[index], rawSurfacePathScratch[index + 1], 0, result);
    }

    private void RefineSurfaceSegment(Collider surfaceCollider, Vector3 startPosition, Vector3 endPosition, int depth, List<Vector3> refinedPositions)
    {
        Vector3 middlePosition = Vector3.Lerp(startPosition, endPosition, 0.5f);
        Vector3 closestSurfacePosition = surfaceCollider.ClosestPoint(middlePosition);
        float middleClearance = Vector3.Distance(middlePosition, closestSurfacePosition);
        bool crossesCollider = middleClearance < surfaceMinimumClearance;

        if (!crossesCollider || depth >= surfacePathRefinementDepth)
        {
            refinedPositions.Add(endPosition);
            return;
        }

        Vector3 projectedMiddle = ProjectOutsidePointToSurface(surfaceCollider, middlePosition);
        RefineSurfaceSegment(surfaceCollider, startPosition, projectedMiddle, depth + 1, refinedPositions);
        RefineSurfaceSegment(surfaceCollider, projectedMiddle, endPosition, depth + 1, refinedPositions);
    }

    private Vector3 ProjectOutsidePointToSurface(Collider surfaceCollider, Vector3 targetPosition)
    {
        Vector3 center = surfaceCollider.bounds.center;
        Vector3 direction = GetSurfaceSafeDirection(targetPosition - center, surfaceCollider.transform.up);
        float outsideDistance = surfaceCollider.bounds.extents.magnitude * 2f + 1f;
        Vector3 outsidePosition = center + direction * outsideDistance;
        Ray surfaceRay = new Ray(outsidePosition, -direction);

        if (surfaceCollider.Raycast(surfaceRay, out RaycastHit surfaceHit, outsideDistance * 2f))
            return surfaceHit.point + surfaceHit.normal * surfaceLineOffset;

        Vector3 fallbackPosition = surfaceCollider.ClosestPoint(outsidePosition);
        Vector3 fallbackNormal = GetSurfaceSafeDirection(outsidePosition - fallbackPosition, direction);
        return fallbackPosition + fallbackNormal * surfaceLineOffset;
    }

    private static float GetSurfaceProjectionDistance(Collider surfaceCollider)
    {
        return Mathf.Max(1f, surfaceCollider.bounds.extents.magnitude * 3f + 1f);
    }

    private Vector3 GetOppositeSurfacePosition(Collider surfaceCollider, Vector3 entryPosition)
    {
        Vector3 center = surfaceCollider.bounds.center;
        Vector3 oppositeDirection = GetSurfaceSafeDirection(center - entryPosition, surfaceCollider.transform.up);
        return ProjectOutsidePointToSurface(surfaceCollider, center + oppositeDirection * (surfaceCollider.bounds.extents.magnitude + 1f));
    }

    private void BuildSurfaceFractalOffsetsNonAlloc(float[] offsets, int segmentCount, int seed)
    {
        System.Array.Clear(offsets, 0, segmentCount + 1);
        uint randomState = CreateSurfaceRandomState(seed);
        SubdivideSurfaceOffsets(offsets, 0, segmentCount, 1f, ref randomState);
        offsets[0] = 0f;
        offsets[segmentCount] = 0f;
    }

    private void SubdivideSurfaceOffsets(float[] offsets, int leftIndex, int rightIndex, float amplitude, ref uint randomState)
    {
        if (rightIndex - leftIndex <= 1)
            return;

        int middleIndex = (leftIndex + rightIndex) / 2;
        float average = (offsets[leftIndex] + offsets[rightIndex]) * 0.5f;
        offsets[middleIndex] = average + (NextSurfaceRandom01(ref randomState) * 2f - 1f) * amplitude;
        float nextAmplitude = amplitude * surfaceFractalDecay;
        SubdivideSurfaceOffsets(offsets, leftIndex, middleIndex, nextAmplitude, ref randomState);
        SubdivideSurfaceOffsets(offsets, middleIndex, rightIndex, nextAmplitude, ref randomState);
    }

    private static void EnsureVectorBuffer(ref Vector3[] buffer, int requiredCount)
    {
        if (buffer != null && buffer.Length >= requiredCount)
            return;

        int capacity = Mathf.NextPowerOfTwo(Mathf.Max(4, requiredCount));
        buffer = new Vector3[capacity];
    }

    private static void EnsureFloatBuffer(ref float[] buffer, int requiredCount)
    {
        if (buffer != null && buffer.Length >= requiredCount)
            return;

        int capacity = Mathf.NextPowerOfTwo(Mathf.Max(4, requiredCount));
        buffer = new float[capacity];
    }

    private static uint CreateSurfaceRandomState(int seed)
    {
        uint state = unchecked((uint)seed) ^ 0xA3C59AC3u;
        return state == 0u ? 1u : state;
    }

    private static float NextSurfaceRandom01(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0x00FFFFFFu) / 16777216f;
    }

    private static int NextSurfaceRandomRange(ref uint state, int minimumInclusive, int maximumExclusive)
    {
        if (maximumExclusive <= minimumInclusive)
            return minimumInclusive;

        float value = NextSurfaceRandom01(ref state);
        int range = maximumExclusive - minimumInclusive;
        return minimumInclusive + Mathf.Min(range - 1, Mathf.FloorToInt(value * range));
    }

    private static Vector3 GetSurfaceSafeDirection(Vector3 direction, Vector3 fallback)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return fallback.normalized;

        return direction.normalized;
    }

    private sealed class SurfaceGeometryFrame
    {
        public Vector3[] MainPositions;
        public int MainCount;
        public Vector3[][] BranchPositions = new Vector3[0][];
        public int[] BranchCounts = new int[0];
        public bool[] BranchVisibility = new bool[0];

        public void EnsureBranchCount(int branchCount)
        {
            if (BranchPositions.Length == branchCount)
                return;

            Vector3[][] previousPositions = BranchPositions;
            BranchPositions = new Vector3[branchCount][];
            BranchCounts = new int[branchCount];
            BranchVisibility = new bool[branchCount];

            int copyCount = Mathf.Min(previousPositions.Length, branchCount);

            for (int index = 0; index < copyCount; index++)
                BranchPositions[index] = previousPositions[index];
        }
    }

    private sealed class SurfaceChainPart
    {
        public StasisElectricVisual Visual { get; private set; }
        public Collider SurfaceCollider { get; private set; }
        public SurfaceChainPart SourcePart { get; private set; }
        public int Depth { get; private set; }

        public void Set(StasisElectricVisual visual, Collider surfaceCollider, SurfaceChainPart sourcePart, int depth)
        {
            Visual = visual;
            SurfaceCollider = surfaceCollider;
            SourcePart = sourcePart;
            Depth = depth;
        }
    }
}
