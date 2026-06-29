using UnityEngine;

public class FollowObject : MonoBehaviour
{
    public Transform target;          // El objeto que cae y rota
    public Vector3 offset = new Vector3(0f, 2f, 0f);  // Altura sobre el objeto

    void LateUpdate()
    {
        if (target == null) return;

        // Solo copiamos posici�n, nunca rotaci�n
        transform.position = target.position + offset;

        if (!target.gameObject.activeSelf) gameObject.gameObject.SetActive(false);
    }
}
