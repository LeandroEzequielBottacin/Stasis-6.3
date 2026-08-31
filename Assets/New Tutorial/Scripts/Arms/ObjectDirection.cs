using UnityEngine;

public class ObjectDirection : MonoBehaviour
{
    [SerializeField] private float lineLength = 3f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Vector3 start = transform.position;
        Vector3 end = start + transform.up *-1* lineLength;

        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, 0.08f);
    }
}