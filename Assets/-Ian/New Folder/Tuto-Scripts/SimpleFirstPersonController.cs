using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovementController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Normal movement speed in meters per second.")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;

    [Tooltip("Movement speed while sprinting.")]
    [SerializeField, Min(0f)] private float sprintSpeed = 8f;

    [Tooltip("Key used to sprint.")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Jump")]
    [Tooltip("Maximum jump height in meters.")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.5f;

    [Tooltip("Gravity acceleration.")]
    [SerializeField] private float gravity = -20f;

    [Tooltip("Small downward velocity maintained while grounded.")]
    [SerializeField] private float groundedVelocity = -2f;

    private CharacterController characterController;

    private float verticalVelocity;

    public bool IsGrounded => characterController != null &&
                              characterController.isGrounded;

    public Vector3 Velocity { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        bool isGrounded = characterController.isGrounded;

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedVelocity;
        }

        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 inputDirection = new Vector3(
            horizontalInput,
            0f,
            verticalInput
        );

        inputDirection = Vector3.ClampMagnitude(
            inputDirection,
            1f
        );

        /*
         * Movement remains relative to the Player orientation.
         *
         * The camera controller rotates the Player horizontally,
         * but this movement component does not need to know why
         * or which camera is currently active.
         */
        Vector3 movementDirection =
            transform.right * inputDirection.x +
            transform.forward * inputDirection.z;

        float currentSpeed = Input.GetKey(sprintKey)
            ? sprintSpeed
            : walkSpeed;

        Vector3 movementVelocity =
            movementDirection * currentSpeed;

        HandleJump(isGrounded);

        verticalVelocity += gravity * Time.deltaTime;

        Velocity = new Vector3(
            movementVelocity.x,
            verticalVelocity,
            movementVelocity.z
        );

        characterController.Move(
            Velocity * Time.deltaTime
        );
    }

    private void HandleJump(bool isGrounded)
    {
        if (!isGrounded)
        {
            return;
        }

        if (!Input.GetButtonDown("Jump"))
        {
            return;
        }

        verticalVelocity =
            Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void OnValidate()
    {
        walkSpeed = Mathf.Max(0f, walkSpeed);
        sprintSpeed = Mathf.Max(0f, sprintSpeed);
        jumpHeight = Mathf.Max(0f, jumpHeight);

        if (gravity >= 0f)
        {
            gravity = -0.01f;
        }

        if (groundedVelocity > 0f)
        {
            groundedVelocity = 0f;
        }
    }
}