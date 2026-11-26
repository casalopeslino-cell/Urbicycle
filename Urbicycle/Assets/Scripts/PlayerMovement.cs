using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float directionSmoothTime = 0.05f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    public InputActionAsset inputActions;
    public Transform cameraTransform;

    private CharacterController controller;
    private CameraFollow cameraFollow;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;

    private float verticalVelocity;
    private Vector3 horizontalVelocity;
    private Vector3 directionSmoothVelocity;
    private bool inputReady;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
        {
            cameraFollow = cameraTransform.GetComponent<CameraFollow>();

            if (cameraFollow == null)
                cameraFollow = cameraTransform.GetComponentInParent<CameraFollow>();
        }

        if (inputActions == null)
            return;

        InputActionMap playerMap = inputActions.FindActionMap("Player", true);

        moveAction = playerMap.FindAction("Move", true);
        jumpAction = playerMap.FindAction("Jump", true);
        sprintAction = playerMap.FindAction("Sprint", true);

        inputReady = true;
    }

    private void OnEnable()
    {
        if (!inputReady)
            return;

        moveAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
    }

    private void OnDisable()
    {
        if (!inputReady)
            return;

        moveAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
    }

    private void Update()
    {
        if (!inputReady)
            return;

        bool grounded = controller.isGrounded;

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (grounded && jumpAction.WasPressedThisFrame())
            verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);

        verticalVelocity += gravity * Time.deltaTime;

        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 direction;

        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            if (cameraFollow != null && cameraFollow.InvertMovement)
            {
                forward = -forward;
                right = -right;
            }

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            direction = forward * input.y + right * input.x;
        }
        else
        {
            direction = new Vector3(input.x, 0f, input.y);
        }

        direction = Vector3.ClampMagnitude(direction, 1f);

        float speed = sprintAction.IsPressed() ? runSpeed : walkSpeed;
        Vector3 targetVelocity = direction * speed;

        if (horizontalVelocity.sqrMagnitude < 0.0001f && targetVelocity.sqrMagnitude > 0.0001f)
        {
            horizontalVelocity = targetVelocity;
            directionSmoothVelocity = Vector3.zero;
        }
        else
        {
            horizontalVelocity = Vector3.SmoothDamp(
                horizontalVelocity,
                targetVelocity,
                ref directionSmoothVelocity,
                directionSmoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        Vector3 velocity = horizontalVelocity;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }
}
