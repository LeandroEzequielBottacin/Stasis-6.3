using UnityEngine;

public sealed class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform rotated horizontally by mouse input.")]
    [SerializeField] private Transform horizontalRotationTarget;

    [Tooltip("Pivot currently receiving vertical camera rotation.")]
    [SerializeField] private Transform activeCameraPivot;

    [Header("Mouse Look")]
    [Tooltip("Mouse sensitivity.")]
    [SerializeField, Min(0f)] private float mouseSensitivity = 2f;

    [Tooltip("Maximum vertical camera rotation.")]
    [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 85f;

    [Tooltip("Invert vertical mouse movement.")]
    [SerializeField] private bool invertY;

    [Header("Cursor")]
    [Tooltip("Lock and hide cursor when Play Mode begins.")]
    [SerializeField] private bool lockCursorOnStart = true;

    [Tooltip("Key used to lock/unlock the cursor.")]
    [SerializeField] private KeyCode cursorToggleKey = KeyCode.Escape;

    private float pitch;

    private bool cursorLocked;

    public Transform ActiveCameraPivot => activeCameraPivot;

    private void Awake()
    {
        if (horizontalRotationTarget == null)
        {
            horizontalRotationTarget = transform;
        }
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    private void Update()
    {
        HandleCursor();

        if (!cursorLocked)
        {
            return;
        }

        HandleMouseLook();
    }

    public void SetCameraPivot(Transform cameraPivot)
    {
        activeCameraPivot = cameraPivot;

        ApplyPitch();
    }

    private void HandleMouseLook()
    {
        if (horizontalRotationTarget == null ||
            activeCameraPivot == null)
        {
            return;
        }

        float mouseX =
            Input.GetAxisRaw("Mouse X") *
            mouseSensitivity;

        float mouseY =
            Input.GetAxisRaw("Mouse Y") *
            mouseSensitivity;

        if (invertY)
        {
            mouseY = -mouseY;
        }

        horizontalRotationTarget.Rotate(
            Vector3.up * mouseX,
            Space.Self
        );

        pitch -= mouseY;

        pitch = Mathf.Clamp(
            pitch,
            -verticalLookLimit,
            verticalLookLimit
        );

        ApplyPitch();
    }

    private void ApplyPitch()
    {
        if (activeCameraPivot == null)
        {
            return;
        }

        activeCameraPivot.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleCursor()
    {
        if (Input.GetKeyDown(cursorToggleKey))
        {
            SetCursorLocked(!cursorLocked);
        }
    }

    private void SetCursorLocked(bool locked)
    {
        cursorLocked = locked;

        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !locked;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || !cursorLocked)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnValidate()
    {
        mouseSensitivity =
            Mathf.Max(0f, mouseSensitivity);
    }
}