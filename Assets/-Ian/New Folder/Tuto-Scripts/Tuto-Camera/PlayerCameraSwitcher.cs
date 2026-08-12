using UnityEngine;

public sealed class PlayerCameraSwitcher : MonoBehaviour
{
    private enum CameraMode
    {
        FirstPerson,
        ThirdPerson
    }

    [Header("Controller")]
    [SerializeField]
    private PlayerCameraController cameraController;

    [Header("First Person")]
    [SerializeField]
    private GameObject firstPersonCameraObject;

    [SerializeField]
    private Transform firstPersonPivot;

    [Header("Third Person")]
    [SerializeField]
    private GameObject thirdPersonCameraObject;

    [SerializeField]
    private Transform thirdPersonPivot;

    [Header("Switch")]
    [Tooltip("Key used to switch between FPS and TPS.")]
    [SerializeField]
    private KeyCode switchKey = KeyCode.V;

    [Tooltip("Camera mode used when Play Mode starts.")]
    [SerializeField]
    private bool startInFirstPerson = true;

    private CameraMode currentMode;

    public bool IsFirstPerson =>
        currentMode == CameraMode.FirstPerson;

    private void Awake()
    {
        currentMode = startInFirstPerson
            ? CameraMode.FirstPerson
            : CameraMode.ThirdPerson;

        ApplyCurrentMode();
    }

    private void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            ToggleCamera();
        }
    }

    public void ToggleCamera()
    {
        currentMode =
            currentMode == CameraMode.FirstPerson
                ? CameraMode.ThirdPerson
                : CameraMode.FirstPerson;

        ApplyCurrentMode();
    }

    public void SetFirstPerson()
    {
        currentMode = CameraMode.FirstPerson;

        ApplyCurrentMode();
    }

    public void SetThirdPerson()
    {
        currentMode = CameraMode.ThirdPerson;

        ApplyCurrentMode();
    }

    private void ApplyCurrentMode()
    {
        bool firstPersonActive =
            currentMode == CameraMode.FirstPerson;

        /*
         * The inactive camera GameObject is fully disabled.
         * Therefore its Camera component does not render.
         */
        if (firstPersonCameraObject != null)
        {
            firstPersonCameraObject.SetActive(
                firstPersonActive
            );
        }

        if (thirdPersonCameraObject != null)
        {
            thirdPersonCameraObject.SetActive(
                !firstPersonActive
            );
        }

        if (cameraController == null)
        {
            return;
        }

        cameraController.SetCameraPivot(
            firstPersonActive
                ? firstPersonPivot
                : thirdPersonPivot
        );
    }
}