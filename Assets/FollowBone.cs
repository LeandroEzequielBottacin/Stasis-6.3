using UnityEngine;

public class FollowBone : MonoBehaviour
{
    [Header("Hueso / Transform a seguir (solo su rotación)")]
    [SerializeField] private Transform targetBone;

    [Header("Clamp de rotación en X (grados)")]
    public float minX = -45f;
    public float maxX = 45f;

    [Tooltip("Offset opcional de rotación (en grados, aplicado antes del clamp)")]
    public Vector3 rotationOffset = Vector3.zero;

    private void LateUpdate()
    {
        if (targetBone == null) return;

        // Guardamos la posición actual para no modificarla
        Vector3 worldPos = transform.position;

        // Rotación del hueso + offset
        Quaternion targetRot = targetBone.rotation * Quaternion.Euler(rotationOffset);

        // Pasamos a euler para poder clamppear X
        Vector3 euler = targetRot.eulerAngles;

        // Normalizamos X al rango -180/180
        float x = euler.x;
        if (x > 180f) x -= 360f;

        // Clamp en X
        x = Mathf.Clamp(x, minX, maxX);

        // Aplicamos la rotación con X clamppeada
        transform.rotation = Quaternion.Euler(x, euler.y, euler.z);

        // Restauramos la posición original
        transform.position = worldPos;
    }
}

