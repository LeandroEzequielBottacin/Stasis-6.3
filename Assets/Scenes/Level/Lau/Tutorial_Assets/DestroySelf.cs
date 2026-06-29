using TMPro;
using UnityEngine;
using UnityEngine.UI;
// Para elementos de UI

// Por si usás TextMeshPro

public class DisableComponents : MonoBehaviour
{
    private MeshRenderer _renderer;
    private Collider _collider;
    private Rigidbody _rigidbody;

    private Light[] _lights;
    private Canvas[] _canvases;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _collider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();

        // Tomamos luces y canvases del objeto y sus hijos
        _lights = GetComponentsInChildren<Light>(true);
        _canvases = GetComponentsInChildren<Canvas>(true);
    }

    public void Destroy()
    {
        if (_renderer)
            _renderer.enabled = false;

        if (_collider)
            _collider.enabled = false;

        if (_rigidbody)
        {
            _rigidbody.useGravity = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }

        // 🔥 Desactivar todas las luces
        foreach (var light in _lights)
        {
            if (light)
                light.enabled = false;
        }

        // 🖥️ Desactivar todo el Canvas y sus elementos
        foreach (var canvas in _canvases)
        {
            if (canvas)
            {
                canvas.enabled = false;

                // Desactivar elementos interactuables también
                DisableCanvasElements(canvas);
            }
        }
    }

    // 🔧 Apaga imágenes, textos, botones, lo que haya en ese canvas
    private void DisableCanvasElements(Canvas canvas)
    {
        var graphics = canvas.GetComponentsInChildren<Graphic>(true);
        foreach (var g in graphics)
            g.enabled = false;

        var tmps = canvas.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in tmps)
            t.enabled = false;

        var buttons = canvas.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
            b.interactable = false;
    }
}


