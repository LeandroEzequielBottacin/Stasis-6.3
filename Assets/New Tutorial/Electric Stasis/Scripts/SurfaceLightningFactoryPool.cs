using System.Collections.Generic;
using UnityEngine;

public class SurfaceLightningFactoryPool : MonoBehaviour
{
    [Header("Factory")]
    [SerializeField] private ProceduralLightning surfaceLightningPrefab;
    [SerializeField] private Transform instancesRoot;

    [Header("Pool")]
    [Min(0)]
    [Tooltip("Cantidad de instancias creadas antes del primer disparo. Para un brazo que puede usar 80 rayos, conviene precalentar las 80 y evitar Instantiate durante el efecto.")]
    [SerializeField] private int initialCapacity = 80;

    [Min(1)]
    [SerializeField] private int maximumCapacity = 80;

    [Tooltip("Si esta activo, crea Maximum Capacity completa en Awake. Evita Instantiate durante el primer disparo a cambio de una carga inicial mayor.")]
    [SerializeField] private bool prewarmMaximumCapacity = true;

    private readonly Queue<ProceduralLightning> availableInstances =
        new Queue<ProceduralLightning>();

    private readonly HashSet<ProceduralLightning> activeInstances =
        new HashSet<ProceduralLightning>();

    private int createdInstanceCount;

    private void Awake()
    {
        maximumCapacity = Mathf.Max(1, maximumCapacity);
        initialCapacity = Mathf.Clamp(initialCapacity, 0, maximumCapacity);

        int preloadCount = prewarmMaximumCapacity ? maximumCapacity : initialCapacity;

        for (int index = 0; index < preloadCount; index++)
        {
            ProceduralLightning instance = CreateInstance();

            if (instance != null)
                availableInstances.Enqueue(instance);
        }
    }

    public ProceduralLightning Get()
    {
        RemoveDestroyedAvailableInstances();

        ProceduralLightning instance;

        if (availableInstances.Count > 0)
        {
            instance = availableInstances.Dequeue();
        }
        else
        {
            if (createdInstanceCount >= maximumCapacity)
                return null;

            instance = CreateInstance();

            if (instance == null)
                return null;
        }

        activeInstances.Add(instance);
        instance.transform.SetParent(GetActiveInstancesRoot(), false);
        instance.SetSurfacePoolOwner(this);
        instance.gameObject.SetActive(true);
        return instance;
    }

    public bool PlaySurfaceLightning(
        Collider surfaceCollider,
        Vector3 entryPosition,
        Vector3 waypointPosition,
        Vector3 exitPosition,
        Vector3 connectionPosition,
        bool includeConnection,
        int generationSeed
    )
    {
        if (surfaceCollider == null)
            return false;

        ProceduralLightning instance = Get();

        if (instance == null)
            return false;

        instance.PlayOnSurface(
            surfaceCollider,
            entryPosition,
            waypointPosition,
            exitPosition,
            connectionPosition,
            includeConnection,
            generationSeed
        );

        return true;
    }

    public void Release(ProceduralLightning instance)
    {
        if (instance == null)
            return;

        if (!activeInstances.Remove(instance))
            return;

        instance.ResetSurfacePoolInstance();
        instance.transform.SetParent(GetActiveInstancesRoot(), false);
        instance.gameObject.SetActive(false);
        availableInstances.Enqueue(instance);
    }

    private ProceduralLightning CreateInstance()
    {
        if (surfaceLightningPrefab == null)
        {
            Debug.LogError(
                "SurfaceLightningFactoryPool: no se asigno el prefab de ProceduralLightning.",
                this
            );

            return null;
        }

        Transform parent = GetActiveInstancesRoot();
        ProceduralLightning instance = Instantiate(surfaceLightningPrefab, parent);
        instance.name = surfaceLightningPrefab.name + " Pooled " + createdInstanceCount;
        instance.SetSurfacePoolOwner(this);
        instance.ResetSurfacePoolInstance();
        instance.gameObject.SetActive(false);
        createdInstanceCount++;
        return instance;
    }

    private void RemoveDestroyedAvailableInstances()
    {
        while (availableInstances.Count > 0 && availableInstances.Peek() == null)
        {
            availableInstances.Dequeue();
            createdInstanceCount = Mathf.Max(0, createdInstanceCount - 1);
        }
    }

    private Transform GetActiveInstancesRoot()
    {
        if (instancesRoot != null && instancesRoot.gameObject.activeInHierarchy)
            return instancesRoot;

        return transform;
    }

    private void OnValidate()
    {
        maximumCapacity = Mathf.Max(1, maximumCapacity);
        initialCapacity = Mathf.Clamp(initialCapacity, 0, maximumCapacity);
    }
}
