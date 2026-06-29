using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[AddComponentMenu("Environment/Podium Rotator with Exposure Fade")]
public class PodiumRotatorWithExposureFade : MonoBehaviour
{
    [Header("=== ROTACIÓN DEL PODIO ===")]
    [Tooltip("Objeto que va a rotar (si se deja vacío, usa este mismo GameObject).")]
    [SerializeField] private Transform rotatingTarget;

    [Tooltip("Eje de rotación.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Tooltip("Velocidad de rotación en grados por segundo.")]
    [SerializeField] private float rotationSpeed = 30f;

    [Tooltip("Espacio de rotación (Local o World).")]
    [SerializeField] private Space rotationSpace = Space.World;

    [Header("=== VOLUME Y EXPOSURE FADE ===")]
    [Tooltip("Global Volume que contiene el Color Adjustments.")]
    [SerializeField] private Volume globalVolume;

    [Tooltip("Valor inicial de Post Exposure.")]
    [SerializeField] private float startExposure = -10f;

    [Tooltip("Valor final de Post Exposure.")]
    [SerializeField] private float endExposure = 0.25f;

    [Tooltip("Duración del fade de exposición en segundos.")]
    [SerializeField] private float fadeDuration = 2f;

    [Tooltip("Curva para controlar la suavidad del fade (0-1 en X, 0-1 en Y).")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private ColorAdjustments _colorAdjustments;
    private bool _hasColorAdjustments = false;

    private bool _canRotate = false;
    private bool _isFading = false;
    private float _fadeTimer = 0f;

    private void Reset()
    {
        // Intentar autocompletar referencias
        rotatingTarget = transform;
    }

    private void Awake()
    {
        if (rotatingTarget == null)
            rotatingTarget = transform;

        // Validar Volume y obtener ColorAdjustments
        if (globalVolume == null)
        {
            Debug.LogWarning("[PodiumRotatorWithExposureFade] No hay Volume asignado. La rotación empezará sin fade.");
            _canRotate = true;
            return;
        }

        if (globalVolume.profile == null)
        {
            Debug.LogWarning("[PodiumRotatorWithExposureFade] El Volume no tiene Profile asignado.");
            _canRotate = true;
            return;
        }

        if (globalVolume.profile.TryGet(out _colorAdjustments))
        {
            _hasColorAdjustments = true;

            // Aseguramos que el override de Post Exposure esté activo
            _colorAdjustments.postExposure.overrideState = true;

            // Seteamos el valor inicial
            _colorAdjustments.postExposure.value = startExposure;

            // Arrancamos el fade
            _fadeTimer = 0f;
            _isFading = true;
            _canRotate = false;
        }
        else
        {
            Debug.LogWarning("[PodiumRotatorWithExposureFade] El Volume Profile no tiene ColorAdjustments. No se puede animar Post Exposure.");
            _canRotate = true;
        }
    }

    private void Update()
    {
        HandleExposureFade();
        HandleRotation();
    }

    private void HandleExposureFade()
    {
        if (!_isFading || !_hasColorAdjustments)
            return;

        if (fadeDuration <= 0f)
        {
            // Sin fade: setea directo y habilita rotación
            _colorAdjustments.postExposure.value = endExposure;
            _isFading = false;
            _canRotate = true;
            return;
        }

        _fadeTimer += Time.deltaTime;
        float t = Mathf.Clamp01(_fadeTimer / fadeDuration);

        // Aplicar curva (0-1 → 0-1)
        float curveT = fadeCurve != null ? fadeCurve.Evaluate(t) : t;

        // Lerp del exposure
        float exposure = Mathf.Lerp(startExposure, endExposure, curveT);
        _colorAdjustments.postExposure.value = exposure;

        if (t >= 1f)
        {
            // Nos aseguramos que quede EXACTAMENTE en el valor final
            _colorAdjustments.postExposure.value = endExposure;

            // El fade terminó
            _isFading = false;

            // Desde este frame en adelante se permite la rotación
            _canRotate = true;
        }
    }

    private void HandleRotation()
    {
        // Solo rota si el fade terminó
        if (!_canRotate || rotatingTarget == null)
            return;

        rotatingTarget.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, rotationSpace);
    }

    #region API PÚBLICA

    /// <summary>
    /// Reinicia el fade de exposición y frena la rotación hasta terminar.
    /// </summary>
    public void RestartExposureFade()
    {
        if (!_hasColorAdjustments) return;

        _fadeTimer = 0f;
        _isFading = true;
        _canRotate = false;
        _colorAdjustments.postExposure.value = startExposure;
    }

    #endregion
}
