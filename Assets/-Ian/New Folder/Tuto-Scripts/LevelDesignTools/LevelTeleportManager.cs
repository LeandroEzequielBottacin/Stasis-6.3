using System.Collections.Generic;
using UnityEngine;

public sealed class LevelTeleportManager : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Transform raíz del Player que será transportado.")]
    [SerializeField] private Transform player;

    [Tooltip(
        "Si está asignado, se desactiva temporalmente durante el teleport " +
        "para evitar conflictos al modificar directamente el Transform.")]
    [SerializeField] private CharacterController characterController;

    [Tooltip(
        "Opcional. Si está asignado, se limpia cualquier velocidad externa " +
        "antes de transportar al Player.")]
    [SerializeField] private PlayerMovementController playerMovement;

    [Header("Teleport Points")]
    [Tooltip("Lista de puntos disponibles para teleport.")]
    [SerializeField] private List<Transform> teleportPoints = new();

    [Tooltip("Índice inicial de la lista.")]
    [SerializeField, Min(0)] private int currentPointIndex = 0;

    [Header("Teleport Settings")]
    [Tooltip("También copia la rotación Y del punto de destino.")]
    [SerializeField] private bool applyRotation = true;

    [Tooltip(
        "Offset vertical adicional aplicado al Player después del teleport. " +
        "Útil para evitar comenzar ligeramente dentro del suelo.")]
    [SerializeField] private float verticalOffset = 0.05f;

    [Header("Controls")]
    [Tooltip("Ir al punto anterior.")]
    [SerializeField] private KeyCode previousPointKey = KeyCode.PageUp;

    [Tooltip("Ir al siguiente punto.")]
    [SerializeField] private KeyCode nextPointKey = KeyCode.PageDown;

    [Tooltip(
        "Si está activo, cambiar de índice transporta inmediatamente al Player.")]
    [SerializeField] private bool teleportImmediatelyWhenCycling = true;

    [Tooltip(
        "Tecla para transportar al Player al punto actualmente seleccionado. " +
        "Solo es necesaria si Teleport Immediately When Cycling está desactivado.")]
    [SerializeField] private KeyCode teleportKey = KeyCode.T;

    [Header("Debug")]
    [SerializeField] private bool logTeleports = true;

    public int CurrentPointIndex => currentPointIndex;

    public int PointCount => teleportPoints.Count;

    private void Awake()
    {
        CacheReferences();

        ClampCurrentIndex();
    }

    private void Update()
    {
        HandleInput();
    }

    private void CacheReferences()
    {
        if (player == null)
        {
            Debug.LogError(
                $"{nameof(LevelTeleportManager)} requires a Player reference.",
                this
            );

            enabled = false;
            return;
        }

        if (characterController == null)
        {
            characterController =
                player.GetComponent<CharacterController>();
        }

        if (playerMovement == null)
        {
            playerMovement =
                player.GetComponent<PlayerMovementController>();
        }
    }

    private void HandleInput()
    {
        if (teleportPoints == null ||
            teleportPoints.Count == 0)
        {
            return;
        }

        if (Input.GetKeyDown(previousPointKey))
        {
            SelectPreviousPoint();

            if (teleportImmediatelyWhenCycling)
            {
                TeleportToCurrentPoint();
            }
        }

        if (Input.GetKeyDown(nextPointKey))
        {
            SelectNextPoint();

            if (teleportImmediatelyWhenCycling)
            {
                TeleportToCurrentPoint();
            }
        }

        if (!teleportImmediatelyWhenCycling &&
            Input.GetKeyDown(teleportKey))
        {
            TeleportToCurrentPoint();
        }
    }

    public void SelectNextPoint()
    {
        if (teleportPoints.Count == 0)
        {
            return;
        }

        currentPointIndex++;

        if (currentPointIndex >= teleportPoints.Count)
        {
            currentPointIndex = 0;
        }
    }

    public void SelectPreviousPoint()
    {
        if (teleportPoints.Count == 0)
        {
            return;
        }

        currentPointIndex--;

        if (currentPointIndex < 0)
        {
            currentPointIndex =
                teleportPoints.Count - 1;
        }
    }

    public void TeleportToCurrentPoint()
    {
        TeleportToPoint(currentPointIndex);
    }

    public void TeleportToPoint(int index)
    {
        if (!TryGetTeleportPoint(index, out Transform target))
        {
            return;
        }

        currentPointIndex = index;

        /*
         * Remove residual forces such as fans, knockback, etc.
         * Otherwise the Player could immediately continue moving
         * after being teleported.
         */
        if (playerMovement != null)
        {
            playerMovement.ClearExternalVelocity();
        }

        bool controllerWasEnabled =
            characterController != null &&
            characterController.enabled;

        /*
         * CharacterController should be disabled while setting
         * the Transform directly.
         */
        if (controllerWasEnabled)
        {
            characterController.enabled = false;
        }

        Vector3 destination =
            target.position +
            Vector3.up * verticalOffset;

        player.position = destination;

        if (applyRotation)
        {
            /*
             * For a typical FPS we only copy yaw.
             * We don't want teleport points rotating the Player
             * sideways or upside-down.
             */
            Vector3 targetEuler =
                target.rotation.eulerAngles;

            player.rotation =
                Quaternion.Euler(
                    0f,
                    targetEuler.y,
                    0f
                );
        }

        if (controllerWasEnabled)
        {
            characterController.enabled = true;
        }

        if (logTeleports)
        {
            Debug.Log(
                $"[Level Teleport] Player moved to " +
                $"[{index}] {target.name}",
                target
            );
        }
    }

    public void TeleportNext()
    {
        SelectNextPoint();
        TeleportToCurrentPoint();
    }

    public void TeleportPrevious()
    {
        SelectPreviousPoint();
        TeleportToCurrentPoint();
    }

    private bool TryGetTeleportPoint(
        int index,
        out Transform point)
    {
        point = null;

        if (teleportPoints == null ||
            teleportPoints.Count == 0)
        {
            Debug.LogWarning(
                $"{nameof(LevelTeleportManager)} has no teleport points configured.",
                this
            );

            return false;
        }

        if (index < 0 ||
            index >= teleportPoints.Count)
        {
            Debug.LogWarning(
                $"Teleport point index {index} is outside the valid range " +
                $"0-{teleportPoints.Count - 1}.",
                this
            );

            return false;
        }

        point = teleportPoints[index];

        if (point == null)
        {
            Debug.LogWarning(
                $"Teleport point [{index}] is null.",
                this
            );

            return false;
        }

        return true;
    }

    private void ClampCurrentIndex()
    {
        if (teleportPoints == null ||
            teleportPoints.Count == 0)
        {
            currentPointIndex = 0;
            return;
        }

        currentPointIndex =
            Mathf.Clamp(
                currentPointIndex,
                0,
                teleportPoints.Count - 1
            );
    }

    private void OnValidate()
    {
        ClampCurrentIndex();

        if (player != null)
        {
            if (characterController == null)
            {
                characterController =
                    player.GetComponent<CharacterController>();
            }

            if (playerMovement == null)
            {
                playerMovement =
                    player.GetComponent<PlayerMovementController>();
            }
        }
    }
}