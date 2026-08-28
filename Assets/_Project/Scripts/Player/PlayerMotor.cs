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

    private Vector3 velocity;
    private bool grounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inventory = GetComponent<PlayerInventory>();
    }

    public void Move(Vector3 direction, bool sprint)
    {
        grounded = controller.isGrounded || CheckGround();

        float speed = sprint ? sprintSpeed : walkSpeed;

        if (inventory != null &&
            inventory.ActiveItem != null &&
            inventory.ActiveItem.IsHeavy)
        {
            speed *= heavyCarrySpeedMultiplier;
        }

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