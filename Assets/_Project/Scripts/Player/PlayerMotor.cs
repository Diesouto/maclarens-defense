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

    private CharacterController controller;
    private PlayerInventory inventory;
    private BodyCarrier bodyCarrier;

    private Vector3 velocity;
    private bool grounded;

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
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
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
        grounded = controller.isGrounded || CheckGround();

        float speed = sprint ? sprintSpeed : walkSpeed;

        bool carryingHeavyItem = inventory != null && inventory.ActiveItem != null && inventory.ActiveItem.IsHeavy;
        bool carryingBody = bodyCarrier != null && bodyCarrier.IsCarryingBody;

        if (carryingHeavyItem || carryingBody)
            speed *= heavyCarrySpeedMultiplier;

        Vector3 movement =
            direction * speed;

        controller.Move(movement * Time.deltaTime);

        grounded = controller.isGrounded || CheckGround();

        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);

        grounded = controller.isGrounded || CheckGround();
    }

    private bool CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float rayDistance = controller.skinWidth + groundCheckDistance;

        return Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.down, out _, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    public void Jump()
    {
        // if (!grounded)
        //     return;

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }
}