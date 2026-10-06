using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(TrainPassenger))]
public class PlayerMotor : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField, Range(0.1f, 1f)]
    private float heavyCarrySpeedMultiplier = 0.5f;

    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -25f;

    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField, Range(30f, 85f)] private float slopeLimit = 60f;
    [SerializeField, Min(0f)] private float stepOffset = 0.4f;
    [Tooltip("Seconds after leaving the ground in which a jump is still accepted.")]
    [SerializeField, Min(0f)] private float coyoteTime = 0.12f;

    private CharacterController controller;
    private PlayerInventory inventory;
    private BodyCarrier bodyCarrier;

    private Vector3 velocity;
    private bool grounded;
    private Vector3 groundNormal = Vector3.up;
    private float lastGroundedTime = float.NegativeInfinity;

    public bool IsGrounded => grounded;

    // CharacterController caches its own position, so direct transform writes get undone on the next Move().
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        // A ragdolled body keeps its CharacterController off; the revive re-enables it.
        controller.enabled = wasEnabled;
        velocity = Vector3.zero;
        if (TryGetComponent(out TrainPassenger passenger))
            passenger.ResetMotion();
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controller.slopeLimit = slopeLimit;
        controller.stepOffset = stepOffset;
        inventory = GetComponent<PlayerInventory>();
        bodyCarrier = GetComponent<BodyCarrier>();
    }

    // Applies the current carriage's per-frame delta (see TrainPassenger) through the same Move()
    // API used for input/gravity, so CharacterController never gets moved outside of a Move() call.
    public void ApplyExternalDisplacement(Vector3 displacement)
    {
        if (displacement.sqrMagnitude > 0f)
            controller.Move(displacement);
    }

    public void Move(Vector3 direction, bool sprint)
    {
        bool probedGround = CheckGround();
        grounded = controller.isGrounded || probedGround;

        float speed = sprint ? sprintSpeed : walkSpeed;

        bool carryingHeavyItem = inventory != null && inventory.ActiveItem != null && inventory.ActiveItem.IsHeavy;
        bool carryingBody = bodyCarrier != null && bodyCarrier.IsCarryingBody;

        if (carryingHeavyItem || carryingBody)
            speed *= heavyCarrySpeedMultiplier;

        Vector3 movement =
            direction * speed;

        // Walking along the ground plane keeps the controller on pitched roofs instead of pushing into them.
        if (grounded && groundNormal.y > 0.05f && groundNormal.y < 0.999f)
            movement = Vector3.ProjectOnPlane(movement, groundNormal);

        controller.Move(movement * Time.deltaTime);

        grounded = controller.isGrounded || CheckGround();

        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);

        grounded = controller.isGrounded || CheckGround();
        if (grounded)
            lastGroundedTime = Time.time;
    }

    private bool CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float rayDistance = controller.skinWidth + groundCheckDistance;

        if (Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            groundNormal = hit.normal;
            return true;
        }

        groundNormal = Vector3.up;
        return false;
    }

    public void Jump()
    {
        if (!grounded && Time.time - lastGroundedTime > coyoteTime)
            return;

        lastGroundedTime = float.NegativeInfinity;
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }
}