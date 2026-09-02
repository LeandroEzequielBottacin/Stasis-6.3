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
        List<SurfaceChainPart> chain = new List<SurfaceChainPart>();
        Transform firstTransform = GetFirstSurfaceStasisTransform(impactedTransform);

        if (firstTransform == null)
            return chain;

        StasisElectricVisual firstVisual = firstTransform.GetComponent<StasisElectricVisual>();

        if (firstVisual == null)
            return chain;

        Collider impactedCollider = impactedTransform.GetComponent<Collider>();
        Collider firstCollider = impactedCollider != null ? impactedCollider : firstVisual.GetSurfaceCollider();

        if (firstCollider == null)
            return chain;

        Queue<SurfaceChainPart> pendingParts = new Queue<SurfaceChainPart>();
        HashSet<StasisElectricVisual> visitedVisuals = new HashSet<StasisElectricVisual>();
        SurfaceChainPart firstPart = new SurfaceChainPart(firstVisual, firstCollider, null, 0);
        pendingParts.Enqueue(firstPart);
        visitedVisuals.Add(firstVisual);

        while (pendingParts.Count > 0)
        {
            SurfaceChainPart currentPart = pendingParts.Dequeue();
            chain.Add(currentPart);
            List<StasisElectricVisual> connectedVisuals = GetConnectedStasisVisuals(currentPart.Visual);

            foreach (StasisElectricVisual connectedVisual in connectedVisuals)
            {
                if (connectedVisual == null || visitedVisuals.Contains(connectedVisual))
                    continue;

                Collider connectedCollider = connectedVisual.GetSurfaceCollider();

                if (connectedCollider == null)
                {
                    if (showSurfaceDebugMessages)
                        Debug.LogWarning(connectedVisual.name + " no tiene un Collider utilizable para la superficie.");

                    continue;
                }

                visitedVisuals.Add(connectedVisual);
                pendingParts.Enqueue(new SurfaceChainPart(connectedVisual, connectedCollider, currentPart, currentPart.Depth + 1));
            }
        }

        return chain;
    }

    private static List<StasisElectricVisual> GetConnectedStasisVisuals(StasisElectricVisual currentVisual)
    {
        List<StasisElectricVisual> connectedVisuals = new List<StasisElectricVisual>();

        if (currentVisual.ConnectionMode == StasisConnectionMode.Arms)
        {
            AddArmParent(currentVisual, connectedVisuals);
            AddArmChildren(currentVisual, connectedVisuals);
        }
        else
        {
            AddFanSiblings(currentVisual, connectedVisuals);
        }

        return connectedVisuals;
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
                yield return new WaitForSeconds(delayBetweenSurfacePieces);
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
        isPlaying = true;
        isMainBoltVisible = true;
        ConfigureSurfaceLineRenderers();
        SetBoltRenderersEnabled(true);
        GenerateSurfaceGeometry();
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
            surfaceGenerationIndex++;
            GenerateSurfaceGeometry();
            surfaceGeometryRefreshTimer = surfaceGeometryRefreshInterval;
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

    private void GenerateSurfaceGeometry()
    {
        if (instanceSurfaceCollider == null)
            return;

        int firstPathSegmentCount = Mathf.Max(2, mainSegmentCount / 2);
        int secondPathSegmentCount = Mathf.Max(2, mainSegmentCount - firstPathSegmentCount);
        Vector3[] firstSurfacePath = GenerateProjectedSurfacePath(instanceSurfaceCollider, instanceEntryPosition, instanceWaypointPosition, firstPathSegmentCount, mainDisplacement * surfaceMainRoughness, surfaceGenerationIndex + noiseSeed);
        Vector3[] secondSurfacePath = GenerateProjectedSurfacePath(instanceSurfaceCollider, instanceWaypointPosition, instanceExitPosition, secondPathSegmentCount, mainDisplacement * surfaceMainRoughness, surfaceGenerationIndex + noiseSeed + 7919);
        Vector3[] surfacePositions = CombineSurfacePaths(firstSurfacePath, secondSurfacePath);

        if (surfacePositions == null || surfacePositions.Length == 0)
            return;

        int connectionPointCount = instanceIncludesConnection ? 1 : 0;
        mainPositions = new Vector3[surfacePositions.Length + connectionPointCount];
        System.Array.Copy(surfacePositions, 0, mainPositions, 0, surfacePositions.Length);

        if (instanceIncludesConnection)
            mainPositions[mainPositions.Length - 1] = instanceConnectionPosition;

        if (mainGlowRenderer != null)
            mainGlowRenderer.positionCount = mainPositions.Length;

        if (mainCoreRenderer != null)
            mainCoreRenderer.positionCount = mainPositions.Length;

        ApplyMainBoltPositions();
        GenerateSurfaceBranches();
    }

    private void GenerateSurfaceBranches()
    {
        if (branchRenderers == null || branchRenderers.Length == 0)
            return;

        System.Random random = new System.Random(surfaceGenerationIndex + noiseSeed * 31);

        for (int branchIndex = 0; branchIndex < branchRenderers.Length; branchIndex++)
        {
            LightningBranchRenderer branch = branchRenderers[branchIndex];

            if (branch == null)
                continue;

            branch.isVisible = random.NextDouble() <= branchVisibleChance;
            SetBranchRendererEnabled(branch, branch.isVisible);

            if (!branch.isVisible)
                continue;

            int lastSurfaceIndex = instanceIncludesConnection ? mainPositions.Length - 2 : mainPositions.Length - 1;

            if (lastSurfaceIndex < 2)
            {
                branch.isVisible = false;
                SetBranchRendererEnabled(branch, false);
                continue;
            }

            int minimumStartIndex = Mathf.Clamp(Mathf.RoundToInt(lastSurfaceIndex * minimumBranchStart), 1, lastSurfaceIndex - 1);
            int maximumStartIndex = Mathf.Clamp(Mathf.RoundToInt(lastSurfaceIndex * maximumBranchStart), minimumStartIndex, lastSurfaceIndex - 1);
            int branchStartIndex = random.Next(minimumStartIndex, maximumStartIndex + 1);
            Vector3 branchStartPosition = mainPositions[branchStartIndex];
            Vector3 center = instanceSurfaceCollider.bounds.center;
            Vector3 surfaceDirection = GetSurfaceSafeDirection(branchStartPosition - center, instanceSurfaceCollider.transform.up);
            CreatePerpendicularAxes(surfaceDirection, out Vector3 surfaceTangent, out Vector3 surfaceBinormal);
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float lengthRatio = Mathf.Lerp(minimumBranchLengthRatio, maximumBranchLengthRatio, (float)random.NextDouble());
            float wrapAmount = surfaceBranchWrapAmount * Mathf.Lerp(0.65f, 1.35f, lengthRatio / Mathf.Max(maximumBranchLengthRatio, 0.001f));
            Vector3 wrappingDirection = surfaceTangent * Mathf.Cos(angle) + surfaceBinormal * Mathf.Sin(angle);
            Vector3 branchEndDirection = (surfaceDirection + wrappingDirection * wrapAmount).normalized;
            Vector3 branchOutsideTarget = center + branchEndDirection * (instanceSurfaceCollider.bounds.extents.magnitude + 1f);
            Vector3 branchEndPosition = ProjectOutsidePointToSurface(instanceSurfaceCollider, branchOutsideTarget);
            branch.positions = GenerateProjectedSurfacePath(instanceSurfaceCollider, branchStartPosition, branchEndPosition, branchSegmentCount, branchDisplacement, surfaceGenerationIndex + branchIndex * 127 + 7001);

            for (int pointIndex = 0; pointIndex < branch.positions.Length; pointIndex++)
                branch.positions[pointIndex] = ProjectOutsidePointToSurface(instanceSurfaceCollider, branch.positions[pointIndex]);

            if (branch.glowRenderer != null)
                branch.glowRenderer.positionCount = branch.positions.Length;

            if (branch.coreRenderer != null)
                branch.coreRenderer.positionCount = branch.positions.Length;

            ApplyBranchPositions(branch);
        }
    }

    private static Vector3[] CombineSurfacePaths(Vector3[] firstPath, Vector3[] secondPath)
    {
        if (firstPath == null || firstPath.Length == 0)
            return secondPath;

        if (secondPath == null || secondPath.Length == 0)
            return firstPath;

        Vector3[] combinedPath = new Vector3[firstPath.Length + secondPath.Length - 1];
        System.Array.Copy(firstPath, 0, combinedPath, 0, firstPath.Length);
        System.Array.Copy(secondPath, 1, combinedPath, firstPath.Length, secondPath.Length - 1);
        return combinedPath;
    }

    private Vector3[] GenerateProjectedSurfacePath(Collider surfaceCollider, Vector3 startPosition, Vector3 endPosition, int segmentCount, float displacement, int seed)
    {
        segmentCount = Mathf.Max(2, segmentCount);
        Vector3[] positions = new Vector3[segmentCount + 1];
        Vector3 center = surfaceCollider.bounds.center;
        Vector3 projectedStart = ProjectOutsidePointToSurface(surfaceCollider, startPosition);
        Vector3 projectedEnd = ProjectOutsidePointToSurface(surfaceCollider, endPosition);
        Vector3 startDirection = GetSurfaceSafeDirection(projectedStart - center, surfaceCollider.transform.up);
        Vector3 endDirection = GetSurfaceSafeDirection(projectedEnd - center, -startDirection);
        float[] firstOffsets = BuildSurfaceFractalOffsets(segmentCount, seed);
        float[] secondOffsets = BuildSurfaceFractalOffsets(segmentCount, seed + 479);
        float outsideDistance = surfaceCollider.bounds.extents.magnitude + 1f;

        for (int index = 0; index <= segmentCount; index++)
        {
            float progress = index / (float)segmentCount;
            float envelope = Mathf.Sin(progress * Mathf.PI);
            Vector3 radialDirection = Vector3.Slerp(startDirection, endDirection, progress).normalized;
            CreatePerpendicularAxes(radialDirection, out Vector3 tangent, out Vector3 binormal);
            Vector3 irregularDirection = radialDirection + tangent * firstOffsets[index] * displacement * envelope + binormal * secondOffsets[index] * displacement * envelope;
            irregularDirection.Normalize();
            Vector3 outsidePosition = center + irregularDirection * outsideDistance;
            positions[index] = ProjectOutsidePointToSurface(surfaceCollider, outsidePosition);
        }

        positions[0] = projectedStart;
        positions[positions.Length - 1] = projectedEnd;
        return RefineSurfacePath(surfaceCollider, positions);
    }

    private Vector3[] RefineSurfacePath(Collider surfaceCollider, Vector3[] originalPositions)
    {
        if (surfacePathRefinementDepth <= 0 || originalPositions == null || originalPositions.Length < 2)
            return originalPositions;

        List<Vector3> refinedPositions = new List<Vector3>();
        refinedPositions.Add(originalPositions[0]);

        for (int index = 0; index < originalPositions.Length - 1; index++)
            RefineSurfaceSegment(surfaceCollider, originalPositions[index], originalPositions[index + 1], 0, refinedPositions);

        return refinedPositions.ToArray();
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

    private float[] BuildSurfaceFractalOffsets(int segmentCount, int seed)
    {
        float[] offsets = new float[segmentCount + 1];
        System.Random random = new System.Random(seed);
        SubdivideSurfaceOffsets(offsets, 0, segmentCount, 1f, random);
        offsets[0] = 0f;
        offsets[offsets.Length - 1] = 0f;
        return offsets;
    }

    private void SubdivideSurfaceOffsets(float[] offsets, int leftIndex, int rightIndex, float amplitude, System.Random random)
    {
        if (rightIndex - leftIndex <= 1)
            return;

        int middleIndex = (leftIndex + rightIndex) / 2;
        float average = (offsets[leftIndex] + offsets[rightIndex]) * 0.5f;
        offsets[middleIndex] = average + ((float)random.NextDouble() * 2f - 1f) * amplitude;
        float nextAmplitude = amplitude * surfaceFractalDecay;
        SubdivideSurfaceOffsets(offsets, leftIndex, middleIndex, nextAmplitude, random);
        SubdivideSurfaceOffsets(offsets, middleIndex, rightIndex, nextAmplitude, random);
    }

    private static Vector3 GetSurfaceSafeDirection(Vector3 direction, Vector3 fallback)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return fallback.normalized;

        return direction.normalized;
    }

    private sealed class SurfaceChainPart
    {
        public StasisElectricVisual Visual { get; }
        public Collider SurfaceCollider { get; }
        public SurfaceChainPart SourcePart { get; }
        public int Depth { get; }

        public SurfaceChainPart(StasisElectricVisual visual, Collider surfaceCollider, SurfaceChainPart sourcePart, int depth)
        {
            Visual = visual;
            SurfaceCollider = surfaceCollider;
            SourcePart = sourcePart;
            Depth = depth;
        }
    }
}
