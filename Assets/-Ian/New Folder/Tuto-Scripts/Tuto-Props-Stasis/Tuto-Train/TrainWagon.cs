using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class TrainWagon : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Movement speed in meters per second.")]
    [SerializeField, Min(0.01f)]
    private float movementSpeed = 8f;

    [Tooltip("Distance at which a waypoint is considered reached.")]
    [SerializeField, Min(0.001f)]
    private float waypointTolerance = 0.05f;

    [Header("Rotation")]
    [Tooltip("Rotate the wagon toward the next waypoint.")]
    [SerializeField]
    private bool orientToMovement = true;

    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField, Min(0f)]
    private float rotationSpeed = 180f;

    [Header("Processed Material")]
    [Tooltip(
        "Renderers whose material should change after the wagon is processed.")]
    [SerializeField]
    private Renderer[] materialRenderers;

    [Tooltip(
        "Material used after the station processes the wagon.")]
    [SerializeField]
    private Material processedMaterial;

    [Tooltip(
        "Restore the original materials whenever this wagon starts a new route. " +
        "Recommended when using wagon pooling.")]
    [SerializeField]
    private bool restoreMaterialOnInitialize = true;

    private Rigidbody rigidbodyReference;

    private TrainRoute route;

    private Transform processingWaypoint;
    private TrainProcessingStation processingStation;

    private int currentWaypointIndex;

    private bool isMoving;
    private bool isProcessing;

    private Material[] originalMaterials;

    public bool IsMoving => isMoving;

    public bool IsProcessing => isProcessing;

    public int CurrentWaypointIndex =>
        currentWaypointIndex;

    public event Action<TrainWagon> RouteCompleted;

    private void Awake()
    {
        rigidbodyReference =
            GetComponent<Rigidbody>();

        rigidbodyReference.isKinematic = true;
        rigidbodyReference.useGravity = false;

        CacheOriginalMaterials();
    }

    private void FixedUpdate()
    {
        if (!isMoving ||
            isProcessing ||
            route == null)
        {
            return;
        }

        MoveTowardCurrentWaypoint();
    }

    /// <summary>
    /// Initializes the wagon and assigns the route plus an optional
    /// processing waypoint/station.
    /// </summary>
    public void Initialize(
        TrainRoute newRoute,
        Transform newProcessingWaypoint,
        TrainProcessingStation newProcessingStation)
    {
        if (newRoute == null ||
            !newRoute.IsValid())
        {
            Debug.LogError(
                $"{nameof(TrainWagon)} received an invalid route.",
                this
            );

            return;
        }

        route = newRoute;

        processingWaypoint =
            newProcessingWaypoint;

        processingStation =
            newProcessingStation;

        currentWaypointIndex = 1;

        isProcessing = false;

        if (restoreMaterialOnInitialize)
        {
            RestoreOriginalMaterials();
        }

        Transform startPoint =
            route.StartPoint;

        rigidbodyReference.position =
            startPoint.position;

        rigidbodyReference.rotation =
            startPoint.rotation;

        rigidbodyReference.angularVelocity =
            Vector3.zero;

        isMoving = true;
    }

    private void MoveTowardCurrentWaypoint()
    {
        Transform target =
            route.GetWaypoint(
                currentWaypointIndex
            );

        if (target == null)
        {
            CompleteRoute();
            return;
        }

        Vector3 currentPosition =
            rigidbodyReference.position;

        Vector3 targetPosition =
            target.position;

        Vector3 toTarget =
            targetPosition -
            currentPosition;

        float distance =
            toTarget.magnitude;

        if (distance <= waypointTolerance)
        {
            rigidbodyReference.position =
                targetPosition;

            HandleWaypointReached(target);

            return;
        }

        float movement =
            movementSpeed *
            Time.fixedDeltaTime;

        Vector3 nextPosition =
            Vector3.MoveTowards(
                currentPosition,
                targetPosition,
                movement
            );

        rigidbodyReference.MovePosition(
            nextPosition
        );

        if (orientToMovement)
        {
            RotateTowardDirection(
                toTarget
            );
        }
    }

    private void HandleWaypointReached(
        Transform reachedWaypoint)
    {
        /*
         * Processing waypoint reached.
         *
         * In your current layout this is Point B.
         */
        if (reachedWaypoint == processingWaypoint &&
            processingStation != null)
        {
            StartCoroutine(
                ProcessStationRoutine()
            );

            return;
        }

        AdvanceToNextWaypoint();
    }

    private IEnumerator ProcessStationRoutine()
    {
        if (isProcessing)
        {
            yield break;
        }

        isProcessing = true;

        /*
         * FixedUpdate movement is now effectively suspended because
         * FixedUpdate checks isProcessing.
         */

        yield return processingStation.ProcessWagon(
            this
        );

        isProcessing = false;

        AdvanceToNextWaypoint();
    }

    private void AdvanceToNextWaypoint()
    {
        currentWaypointIndex++;

        if (currentWaypointIndex >=
            route.WaypointCount)
        {
            CompleteRoute();
        }
    }

    private void RotateTowardDirection(
        Vector3 direction)
    {
        if (direction.sqrMagnitude <=
            Mathf.Epsilon)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        Quaternion nextRotation =
            Quaternion.RotateTowards(
                rigidbodyReference.rotation,
                targetRotation,
                rotationSpeed *
                Time.fixedDeltaTime
            );

        rigidbodyReference.MoveRotation(
            nextRotation
        );
    }

    public void ApplyProcessedMaterial()
    {
        if (processedMaterial == null ||
            materialRenderers == null)
        {
            return;
        }

        for (int i = 0;
             i < materialRenderers.Length;
             i++)
        {
            Renderer targetRenderer =
                materialRenderers[i];

            if (targetRenderer == null)
            {
                continue;
            }

            /*
             * sharedMaterial avoids creating a runtime material
             * instance and unnecessary allocations.
             */
            targetRenderer.sharedMaterial =
                processedMaterial;
        }
    }

    public void RestoreOriginalMaterials()
    {
        if (materialRenderers == null ||
            originalMaterials == null)
        {
            return;
        }

        int count =
            Mathf.Min(
                materialRenderers.Length,
                originalMaterials.Length
            );

        for (int i = 0; i < count; i++)
        {
            if (materialRenderers[i] == null)
            {
                continue;
            }

            materialRenderers[i].sharedMaterial =
                originalMaterials[i];
        }
    }

    private void CacheOriginalMaterials()
    {
        if (materialRenderers == null)
        {
            originalMaterials =
                Array.Empty<Material>();

            return;
        }

        originalMaterials =
            new Material[
                materialRenderers.Length
            ];

        for (int i = 0;
             i < materialRenderers.Length;
             i++)
        {
            Renderer targetRenderer =
                materialRenderers[i];

            if (targetRenderer != null)
            {
                originalMaterials[i] =
                    targetRenderer.sharedMaterial;
            }
        }
    }

    private void CompleteRoute()
    {
        if (!isMoving)
        {
            return;
        }

        isMoving = false;
        isProcessing = false;

        RouteCompleted?.Invoke(this);
    }

    public void Stop()
    {
        isMoving = false;
        isProcessing = false;

        StopAllCoroutines();
    }

    private void OnValidate()
    {
        movementSpeed =
            Mathf.Max(
                0.01f,
                movementSpeed
            );

        waypointTolerance =
            Mathf.Max(
                0.001f,
                waypointTolerance
            );

        rotationSpeed =
            Mathf.Max(
                0f,
                rotationSpeed
            );
    }
}