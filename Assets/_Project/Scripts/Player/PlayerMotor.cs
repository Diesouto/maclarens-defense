using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField, Range(0.1f, 1f)] private float heavyCarrySpeedMultiplier = 0.5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private float groundedMomentumDamping = 8f;

    private CharacterController controller;
    private PlayerInventory inventory;

    private Vector3 velocity;
    private Vector3 carriedVelocity;
    private Vector3 platformVelocity;
    private bool grounded;
    private int lastPlatformVelocityFrame = int.MinValue;
    private Vector3 pendingPlatformVelocity;
    private float pendingPlatformScore = float.PositiveInfinity;
    private int pendingPlatformVelocityFrame = int.MinValue;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inventory = GetComponent<PlayerInventory>();
    }

    public void Move(Vector3 direction, bool sprint)
    {
        CommitPendingPlatformVelocity();
        grounded = controller.isGrounded || CheckGround();
        RefreshCarriedVelocity();

        float speed = sprint ? sprintSpeed : walkSpeed;

        if (inventory != null && inventory.ActiveItem != null && inventory.ActiveItem.IsHeavy)
            speed *= heavyCarrySpeedMultiplier;

        controller.Move((direction * speed + carriedVelocity) * Time.deltaTime);

        grounded = controller.isGrounded || CheckGround();

        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
        grounded = controller.isGrounded || CheckGround();
    }

    public void SetPlatformVelocity(Vector3 newVelocity)
    {
        platformVelocity = newVelocity;
        lastPlatformVelocityFrame = Time.frameCount;
    }

    public void RegisterPlatformVelocity(Vector3 newVelocity, float score)
    {
        if (pendingPlatformVelocityFrame != Time.frameCount)
        {
            pendingPlatformVelocityFrame = Time.frameCount;
            pendingPlatformScore = float.PositiveInfinity;
        }

        if (score >= pendingPlatformScore)
            return;

        pendingPlatformScore = score;
        pendingPlatformVelocity = newVelocity;
    }

    private void CommitPendingPlatformVelocity()
    {
        if (pendingPlatformVelocityFrame < 0)
            return;

        platformVelocity = pendingPlatformVelocity;
        lastPlatformVelocityFrame = pendingPlatformVelocityFrame;
    }

    private void RefreshCarriedVelocity()
    {
        bool hasPlatformVelocity = Time.frameCount - lastPlatformVelocityFrame <= 1;
        if (hasPlatformVelocity)
        {
            carriedVelocity = platformVelocity;
            return;
        }

        if (!grounded)
            return;

        Vector3 horizontalCarry = Vector3.ProjectOnPlane(carriedVelocity, Vector3.up);
        horizontalCarry = Vector3.MoveTowards(horizontalCarry, Vector3.zero, groundedMomentumDamping * Time.deltaTime);
        carriedVelocity = horizontalCarry;
    }

    private bool CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        float rayDistance = controller.skinWidth + groundCheckDistance;

        return Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.down, out _, rayDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    public void Jump()
    {
        if (!grounded)
            return;

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }
}