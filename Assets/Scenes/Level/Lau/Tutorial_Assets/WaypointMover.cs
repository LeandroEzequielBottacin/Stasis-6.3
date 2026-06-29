using UnityEngine;

public class WaypointMover : MonoBehaviour
{
    [Header("Waypoints (agregá los transforms acá)")]
    public Transform[] waypoints;

    [Header("Velocidad de movimiento")]
    public float moveSpeed = 3f;

    [Header("Distancia mínima para considerar que llegó")]
    public float reachThreshold = 0.2f;

    private int currentIndex;

    private void Update()
    {
        if (waypoints.Length == 0) return;

        MoveTowardsWaypoint();
    }

    private void MoveTowardsWaypoint()
    {
        Transform target = waypoints[currentIndex];

        // Moverse hacia el waypoint
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            moveSpeed * Time.deltaTime
        );

        // Si llegó, rota instantáneamente y pasa al siguiente
        if (Vector3.Distance(transform.position, target.position) <= reachThreshold)
        {
            // Rotación instantánea -90 en Y
            transform.Rotate(0, -180f, 0, Space.Self);

            // Ir al siguiente waypoint en bucle
            currentIndex = (currentIndex + 1) % waypoints.Length;
        }
    }
}

