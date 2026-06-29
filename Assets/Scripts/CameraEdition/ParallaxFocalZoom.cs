using UnityEngine;

[AddComponentMenu("Camera/Parallax Focal Zoom")]
[RequireComponent(typeof(Camera))]
public class ParallaxFocalZoom : MonoBehaviour
{
    public enum ZoomMode
    {
        AutoPingPong,   // Zoom eterno automático
        InputControlled // Zoom controlado por input + opcional auto
    }

    [Header("=== MODO DE ZOOM ===")]
    public ZoomMode zoomMode = ZoomMode.AutoPingPong;

    [Tooltip("Si está activo, se suma al valor base un movimiento sinusoidal eterno.")]
    public bool addAutoBreathing = true;

    [Header("=== RANGO DE FOCALES (mm) ===")]
    [Tooltip("Focal mínima (más angular, más deformación de perspectiva).")]
    [Min(1f)] public float minFocalLength = 18f;

    [Tooltip("Focal máxima (más tele, comprime planos, más parallax).")]
    [Min(1f)] public float maxFocalLength = 85f;

    [Header("=== ANIMACIÓN AUTOMÁTICA ===")]
    [Tooltip("Velocidad del ping-pong automático.")]
    [Min(0f)] public float autoSpeed = 0.2f;

    [Tooltip("Intensidad del 'breathing' (oscilación suave sobre el valor base).")]
    [Range(0f, 1f)] public float breathingAmplitude = 0.2f;

    [Header("=== CONTROL POR INPUT ===")]
    [Tooltip("Sensibilidad del zoom al usar la rueda del mouse.")]
    public float scrollSensitivity = 20f;

    [Tooltip("Suavizado del zoom por input (lerp).")]
    [Range(0.01f, 1f)] public float inputLerpSpeed = 0.2f;

    private Camera _cam;
    private float _baseZoomT = 0.5f;        // 0 = minFocal, 1 = maxFocal
    private float _inputZoomT = 0.5f;       // Target por input
    private float _time;

    private void Reset()
    {
        _cam = GetComponent<Camera>();
        EnsurePhysicalCamera();
        // Intentar setear un rango razonable según la focal actual
        if (_cam != null)
        {
            float f = Mathf.Clamp(_cam.focalLength, 10f, 60f);
            minFocalLength = Mathf.Max(10f, f * 0.5f);
            maxFocalLength = Mathf.Max(minFocalLength + 10f, f * 1.5f);
        }
    }

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        EnsurePhysicalCamera();

        // Inicializar zoomT basado en la focal actual
        float current = Mathf.Clamp(_cam.focalLength, minFocalLength, maxFocalLength);
        _baseZoomT = Mathf.InverseLerp(minFocalLength, maxFocalLength, current);
        _inputZoomT = _baseZoomT;
    }

    private void EnsurePhysicalCamera()
    {
        if (_cam == null) return;

        // Activar modo físico para usar focalLength
        _cam.usePhysicalProperties = true;
    }

    private void Update()
    {
        if (_cam == null) return;

        _time += Time.deltaTime;

        // 1) Actualizar T base según modo
        if (zoomMode == ZoomMode.AutoPingPong)
        {
            // Ping-pong lento: 0..1..0..1...
            _baseZoomT = 0.5f + 0.5f * Mathf.Sin(_time * autoSpeed * Mathf.PI * 2f);
        }
        else if (zoomMode == ZoomMode.InputControlled)
        {
            HandleInput();
            // Lerp suave del valor base hacia el target de input
            _baseZoomT = Mathf.Lerp(_baseZoomT, _inputZoomT, inputLerpSpeed);
        }

        // 2) Breathing opcional
        float finalT = _baseZoomT;
        if (addAutoBreathing && breathingAmplitude > 0f)
        {
            float breathing = Mathf.Sin(_time * autoSpeed * 2f * Mathf.PI) * breathingAmplitude;
            finalT = Mathf.Clamp01(finalT + breathing);
        }

        // 3) Convertir T → focalLength
        float focal = Mathf.Lerp(minFocalLength, maxFocalLength, finalT);
        _cam.focalLength = focal;
    }

    private void HandleInput()
    {
        // Mouse wheel en Input Manager: eje "Mouse ScrollWheel"
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            // Aumentamos o disminuimos T según scroll
            _inputZoomT = Mathf.Clamp01(_inputZoomT + scroll * scrollSensitivity * Time.deltaTime);
        }
    }
}

