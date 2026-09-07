using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerPoseController))]
[RequireComponent(typeof(ItemHolder))]
public class PlayerController : MonoBehaviour
{
    // Lets gameplay code (e.g. enemy targeting) find players without FindGameObjectsWithTag.
    private static readonly List<PlayerController> activePlayers = new();
    public static IReadOnlyList<PlayerController> ActivePlayers => activePlayers;

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float lookSensitivity = 1f;
    [SerializeField] private float lookSmoothing = 10f;
    [SerializeField] private float maxLookPitch = 80f;
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float aimFov = 45f;
    [SerializeField] private float zoomSpeed = 40f;
    [SerializeField] private float throwChargeDuration = 5f;
    [SerializeField] private float minimumThrowForce = 2f;
    [SerializeField] private float maximumThrowForce = 10f;
    [SerializeField] private InteractUI interactUI;

    private PlayerInputHandler input;
    private PlayerMotor motor;
    private TrainPassenger trainPassenger;
    private PlayerInventory inventory;
    private Weapon weapon;
    private Health health;
    private Animator animator;
    private float yaw;
    private float pitch;
    private float smoothedYaw;
    private float smoothedPitch;
    private float currentFov;
    private float throwChargeStartedAt;
    private bool isChargingThrow;
    private ItemHolder itemHolder;

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        motor = GetComponent<PlayerMotor>();
        trainPassenger = GetComponent<TrainPassenger>();
        inventory = GetComponent<PlayerInventory>();
        itemHolder = GetComponent<ItemHolder>();
        weapon = itemHolder != null ? itemHolder.RuntimeWeapon : GetComponentInChildren<Weapon>(true);
        health = GetComponent<Health>();
        animator = GetComponentInChildren<Animator>();

        if (interactUI == null)
            interactUI = FindFirstObjectByType<InteractUI>();

        if (health != null)
        {
            health.OnHit += OnHit;
            health.OnDeath += OnDeath;
        }

        if (cameraTransform == null)
            cameraTransform = Camera.main != null ? Camera.main.transform : null;

        if (mainCamera == null)
            mainCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;

        yaw = transform.eulerAngles.y;
        pitch = cameraTransform != null ? NormalizeAngle(cameraTransform.localEulerAngles.x) : 0f;
        smoothedYaw = yaw;
        smoothedPitch = pitch;
        currentFov = mainCamera != null ? mainCamera.fieldOfView : normalFov;

        if (mainCamera != null)
            mainCamera.fieldOfView = currentFov;
        else
            Debug.LogWarning("PlayerController: No camera found for look/zoom.");
    }

    private void OnEnable()
    {
        activePlayers.Add(this);
    }

    private void OnDisable()
    {
        activePlayers.Remove(this);
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
        HandleAimZoom();
        HandleInventoryInput();
        HandleWeaponInput();

        if (input.JumpPressed)
            motor.Jump();
    }

    // Rides the train without parenting: applies the carriage's per-frame position delta through
    // the motor's CharacterController.Move(), and adds the yaw delta directly (no smoothing) so the
    // camera turns in lockstep with the train instead of staying pinned to a world-absolute heading.
    // Runs in LateUpdate (after train followers, see [DefaultExecutionOrder] on their scripts, and
    // after this frame's input movement) so it always reads this frame's up-to-date carriage pose.
    private void ApplyTrainMotion()
    {
        if (trainPassenger == null)
            return;

        if (!trainPassenger.TryGetCarriageDelta(transform.position, motor.IsGrounded, out Vector3 deltaPosition, out float deltaYaw))
            return;

        motor.ApplyExternalDisplacement(deltaPosition);

        yaw += deltaYaw;
        smoothedYaw += deltaYaw;
    }

    private void LateUpdate()
    {
        ApplyTrainMotion();
        ApplyLookRotation();
    }

    private void HandleLook()
    {
        Vector2 lookDelta = input.Look;

        if (lookDelta.sqrMagnitude <= 0.0001f)
            return;

        float scaledSensitivity = lookSensitivity * Time.deltaTime;
        yaw += lookDelta.x * scaledSensitivity;
        pitch = Mathf.Clamp(pitch - lookDelta.y * scaledSensitivity, -maxLookPitch, maxLookPitch);
    }

    private void ApplyLookRotation()
    {
        smoothedYaw = Mathf.LerpAngle(smoothedYaw, yaw, Time.deltaTime * lookSmoothing);
        smoothedPitch = Mathf.LerpAngle(smoothedPitch, pitch, Time.deltaTime * lookSmoothing);

        transform.rotation = Quaternion.Euler(0f, smoothedYaw, 0f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(smoothedPitch, 0f, 0f);
    }

    private void HandleAimZoom()
    {
        if (mainCamera == null)
            return;

        float targetFov = input.AimHeld ? aimFov : normalFov;
        currentFov = Mathf.MoveTowards(currentFov, targetFov, zoomSpeed * Time.deltaTime);
        mainCamera.fieldOfView = currentFov;
    }

    private void HandleMovement()
    {
        Vector2 move = input.Move;

        Vector3 direction = transform.forward * move.y + transform.right * move.x;

        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        bool sprinting = input.SprintHeld && (inventory == null || inventory.ActiveItem == null || !inventory.ActiveItem.IsHeavy);
        motor.Move(direction, sprinting);

        if (animator != null)
        {
            float moveMagnitude = move.magnitude;
            float speedValue = moveMagnitude <= 0.01f ? 0f : sprinting ? 1f : 0.25f;
            animator.SetFloat("Speed", speedValue);
        }
    }

    private void HandleInventoryInput()
    {
        if (inventory == null)
            return;

        if (input.DropPressed && inventory.ActiveItem != null)
        {
            isChargingThrow = true;
            throwChargeStartedAt = Time.time;
            interactUI?.StartHeldProgress(throwChargeDuration, "Throwing...");
            return;
        }

        if (isChargingThrow && input.DropReleased)
        {
            ReleaseThrow();
            return;
        }

        if (isChargingThrow && input.DropHeld)
            return;

        if (input.Number1Pressed)
        {
            CancelThrow();
            inventory.TrySelectSlotByNumber(1);
        }
        else if (input.Number2Pressed)
        {
            CancelThrow();
            inventory.TrySelectSlotByNumber(2);
        }
        else if (input.Number3Pressed)
        {
            CancelThrow();
            inventory.TrySelectSlotByNumber(3);
        }
        else if (input.Number4Pressed)
        {
            CancelThrow();
            inventory.TrySelectSlotByNumber(4);
        }

    }

    private void HandleWeaponInput()
    {
        if (inventory == null || inventory.ActiveItem == null || !inventory.ActiveItem.IsWeapon)
            return;

        weapon = itemHolder != null ? itemHolder.RuntimeWeapon : weapon;
        if (!inventory.TryUseActiveItem(weapon) || weapon == null)
            return;

        weapon.HandleInput(input.FirePressed, input.FireHeld, input.ReloadPressed);
    }

    private void ReleaseThrow()
    {
        float charge = Mathf.Clamp01((Time.time - throwChargeStartedAt) / Mathf.Max(throwChargeDuration, 0.01f));
        float force = Mathf.Lerp(minimumThrowForce, maximumThrowForce, charge);
        Transform throwPoint = itemHolder != null ? itemHolder.ThrowPoint : transform;
        Vector3 position = throwPoint.position + transform.forward * 0.2f;
        Vector3 throwDirection = mainCamera != null ? mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f)).direction : transform.forward;
        Vector3 throwForce = throwDirection * force;
        isChargingThrow = false;
        interactUI?.FinishProgress();
        inventory.TryThrowSelected(position, throwForce);
    }

    private void CancelThrow()
    {
        if (!isChargingThrow)
            return;

        isChargingThrow = false;
        interactUI?.CancelProgress();
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnHit -= OnHit;
            health.OnDeath -= OnDeath;
        }
    }

    private void OnHit()
    {
        if (animator != null)
            animator.SetTrigger("HasBeenHit");
    }

    private void OnDeath()
    {
        if (input != null)
            input.enabled = false;

        if (motor != null)
            motor.enabled = false;

        enabled = false;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}
