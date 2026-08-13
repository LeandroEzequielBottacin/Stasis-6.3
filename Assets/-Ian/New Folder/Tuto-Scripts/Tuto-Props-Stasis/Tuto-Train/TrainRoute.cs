using System.Collections.Generic;
using UnityEngine;

public sealed class TrainRoute : MonoBehaviour
{
    [Header("Route")]
    [Tooltip("Waypoints in traversal order. Element 0 is the spawn point.")]
    [SerializeField] private List<Transform> waypoints = new();

    public int WaypointCount => waypoints.Count;

    public Transform StartPoint =>
        waypoints.Count > 0
            ? waypoints[0]
            : null;

    public Transform EndPoint =>
        waypoints.Count > 0
            ? waypoints[waypoints.Count - 1]
            : null;

    public Transform GetWaypoint(int index)
    {
        if (index < 0 || index >= waypoints.Count)
        {
            return null;
        }

        return waypoints[index];
    }

    public bool IsValid()
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            return false;
        }

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            return;
        }

        for (int i = 0; i < waypoints.Count; i++)
        {
            Transform point = waypoints[i];

            if (point == null)
            {
                continue;
            }

            Gizmos.DrawWireSphere(point.position, 0.25f);

            if (i >= waypoints.Count - 1)
            {
                continue;
            }

            Transform nextPoint = waypoints[i + 1];

            if (nextPoint != null)
            {
                Gizmos.DrawLine(
                    point.position,
                    nextPoint.position
                );
            }
        }
    }
}