using UnityEngine;

public class ClampRotationY : MonoBehaviour
{
    [Header("Límites de rotación en X (grados)")]
    public float minX = -45f;
    public float maxX = 45f;

    private void LateUpdate()
    {
        // Tomamos la rotación actual en Euler
        Vector3 currentRotation = transform.eulerAngles;

        // Convertimos el ángulo X a rango -180 / 180 para evitar saltos
        float x = currentRotation.x;
        if (x > 180f) x -= 360f;

        // Aplicamos clamp
        x = Mathf.Clamp(x, minX, maxX);

        // Asignamos la rotación nuevamente
        transform.rotation = Quaternion.Euler(x, currentRotation.y, currentRotation.z);
    }
}


