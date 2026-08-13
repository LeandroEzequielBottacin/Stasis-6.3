using System.Collections;
using UnityEngine;

public sealed class TrainManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Wagon prefab containing a TrainWagon component.")]
    [SerializeField] private TrainWagon wagonPrefab;

    [Tooltip("Route followed by spawned wagons.")]
    [SerializeField] private TrainRoute route;

    [Header("Spawn")]
    [Tooltip("Automatically start spawning when Play Mode begins.")]
    [SerializeField] private bool spawnOnStart = true;

    [Tooltip(
        "Delay after a wagon reaches the final point before spawning the next one.")]
    [SerializeField, Min(0f)] private float spawnDelay = 1f;

    [Header("Optimization")]
    [Tooltip(
        "Reuse the same wagon instead of Instantiate/Destroy every cycle. " +
        "Recommended for repeated trains.")]
    [SerializeField] private bool reuseWagonInstance = true;

    [Header("Processing Station")]
    [Tooltip("Waypoint where the wagon must stop for processing.")]
    [SerializeField]
    private Transform processingWaypoint;

    [Tooltip("Station responsible for the processing sequence.")]
    [SerializeField]
    private TrainProcessingStation processingStation;

    private TrainWagon activeWagon;
    private TrainWagon pooledWagon;

    private Coroutine spawnCoroutine;

    private bool isRunning;

    public bool IsRunning => isRunning;

    public TrainWagon ActiveWagon => activeWagon;

    private void Start()
    {
        if (spawnOnStart)
        {
            StartTrainSystem();
        }
    }

    public void StartTrainSystem()
    {
        if (isRunning)
        {
            return;
        }

        if (!ValidateConfiguration())
        {
            return;
        }

        isRunning = true;

        SpawnWagon();
    }

    public void StopTrainSystem()
    {
        isRunning = false;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        if (activeWagon != null)
        {
            activeWagon.RouteCompleted -=
                HandleWagonCompleted;

            activeWagon.Stop();

            DisableOrDestroyWagon(
                activeWagon
            );

            activeWagon = null;
        }
    }

    private void SpawnWagon()
    {
        /*
         * Critical protection:
         *
         * Never spawn a second wagon while another one
         * is still active on this route.
         */
        if (!isRunning || activeWagon != null)
        {
            return;
        }

        TrainWagon wagon =
            GetWagonInstance();

        if (wagon == null)
        {
            return;
        }

        activeWagon = wagon;

        activeWagon.RouteCompleted +=
            HandleWagonCompleted;

        activeWagon.Initialize(
            route,
            processingWaypoint,
            processingStation);
    }

    private TrainWagon GetWagonInstance()
    {
        if (reuseWagonInstance)
        {
            if (pooledWagon == null)
            {
                pooledWagon =
                    Instantiate(
                        wagonPrefab,
                        route.StartPoint.position,
                        route.StartPoint.rotation
                    );
            }

            pooledWagon.gameObject.SetActive(true);

            return pooledWagon;
        }

        return Instantiate(
            wagonPrefab,
            route.StartPoint.position,
            route.StartPoint.rotation
        );
    }

    private void HandleWagonCompleted(
        TrainWagon wagon)
    {
        if (wagon == null ||
            wagon != activeWagon)
        {
            return;
        }

        wagon.RouteCompleted -=
            HandleWagonCompleted;

        /*
         * At this exact moment the wagon has reached C.
         *
         * Only now do we allow another wagon to be scheduled.
         */
        DisableOrDestroyWagon(wagon);

        activeWagon = null;

        if (!isRunning)
        {
            return;
        }

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        spawnCoroutine =
            StartCoroutine(
                SpawnNextWagonRoutine()
            );
    }

    private IEnumerator SpawnNextWagonRoutine()
    {
        if (spawnDelay > 0f)
        {
            yield return new WaitForSeconds(
                spawnDelay
            );
        }

        spawnCoroutine = null;

        /*
         * Double-check in case something spawned a wagon
         * through another public call.
         */
        if (isRunning && activeWagon == null)
        {
            SpawnWagon();
        }
    }

    private void DisableOrDestroyWagon(
        TrainWagon wagon)
    {
        if (wagon == null)
        {
            return;
        }

        if (reuseWagonInstance)
        {
            wagon.gameObject.SetActive(false);
        }
        else
        {
            Destroy(
                wagon.gameObject
            );
        }
    }

    private bool ValidateConfiguration()
    {
        if (wagonPrefab == null)
        {
            Debug.LogError(
                $"{nameof(TrainManager)} requires a Wagon Prefab.",
                this
            );

            return false;
        }

        if (route == null)
        {
            Debug.LogError(
                $"{nameof(TrainManager)} requires a TrainRoute.",
                this
            );

            return false;
        }

        if (!route.IsValid())
        {
            Debug.LogError(
                $"{nameof(TrainManager)} has an invalid route. " +
                "At least two non-null waypoints are required.",
                route
            );

            return false;
        }

        return true;
    }

    private void OnValidate()
    {
        spawnDelay =
            Mathf.Max(
                0f,
                spawnDelay
            );
    }
}