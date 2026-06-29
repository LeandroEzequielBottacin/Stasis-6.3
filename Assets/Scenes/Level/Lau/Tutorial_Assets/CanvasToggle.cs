using UnityEngine;

public class CanvasToggle : MonoBehaviour
{
    private Canvas _canvas;

    private void Awake()
    {
        // Obtiene el Canvas del mismo GameObject
        _canvas = GetComponent<Canvas>();

        if (_canvas == null)
            Debug.LogWarning("No se encontró un Canvas en este GameObject.");
    }

    public void ShowCanvas()
    {
        _canvas.enabled = true;
    }

    public void HideCanvas()
    {
        _canvas.enabled = false;
    }
}
