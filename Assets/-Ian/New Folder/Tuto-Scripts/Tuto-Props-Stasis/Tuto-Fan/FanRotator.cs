using UnityEngine;

public sealed class FanRotator : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("Local axis around which the fan rotates.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.forward;

    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 360f;

    [Tooltip("If enabled, the fan starts rotating automatically.")]
    [SerializeField] private bool rotateOnStart = true;

    [Header("Settings")]
    [Tooltip("Rotate using local space. Recommended for fans.")]
    [SerializeField] private Space rotationSpace = Space.Self;

    private bool isRotating;

    /// <summary>
    /// Returns whether the fan is currently rotating.
    /// </summary>
    public bool IsRotating => isRotating;

    /// <summary>
    /// Current rotation speed in degrees per second.
    /// Negative values rotate in the opposite direction.
    /// </summary>
    public float RotationSpeed
    {
        get => rotationSpeed;
        set => rotationSpeed = value;
    }

    private void Awake()
    {
        isRotating = rotateOnStart;
    }

    private void Update()
    {
        if (!isRotating)
        {
            return;
        }

        Rotate();
    }

    private void Rotate()
    {
        if (rotationAxis.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        transform.Rotate(
            rotationAxis.normalized,
            rotationSpeed * Time.deltaTime,
            rotationSpace
        );
    }

    /// <summary>
    /// Starts the fan rotation.
    /// </summary>
    public void StartRotation()
    {
        isRotating = true;
    }

    /// <summary>
    /// Stops the fan rotation.
    /// </summary>
    public void StopRotation()
    {
        isRotating = false;
    }

    /// <summary>
    /// Enables or disables rotation.
    /// </summary>
    public void SetRotation(bool enabled)
    {
        isRotating = enabled;
    }

    /// <summary>
    /// Toggles the current rotation state.
    /// </summary>
    public void ToggleRotation()
    {
        isRotating = !isRotating;
    }

    /// <summary>
    /// Sets the rotation speed in degrees per second.
    /// </summary>
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = speed;
    }

    /// <summary>
    /// Reverses the current rotation direction.
    /// </summary>
    public void ReverseDirection()
    {
        rotationSpeed = -rotationSpeed;
    }

    private void OnValidate()
    {
        if (rotationAxis.sqrMagnitude <= Mathf.Epsilon)
        {
            rotationAxis = Vector3.forward;
        }
    }
}