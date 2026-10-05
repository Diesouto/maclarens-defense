using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerMotor))]
[RequireComponent(typeof(PlayerPoseController))]
[RequireComponent(typeof(ItemHolder))]
public class PlayerController : MonoBehaviour
{
    // Every spawned player (local and remote copies); remote PlayerControllers are disabled, so this
    // registers in Awake/OnDestroy. Consumers must filter with IsAlive.
    private static readonly List<PlayerController> activePlayers = new();
    public static IReadOnlyList<PlayerController> ActivePlayers => activePlayers;

    public bool IsAlive => gameObject.activeInHierarchy && (health == null || !health.IsDead) &&
        (body == null || !body.IsHidden);

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Camera mainCamera;
    [Tooltip("Head/neck ragdoll bone the camera reparents to on death, so it rides the ragdoll physics. Leave cameraTransform parented to a static pivot (not this bone) for normal play, otherwise the bone's bind-pose rotation breaks ApplyLookRotation's math.")]
    [SerializeField] private Transform ragdollCameraAnchor;
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

    [Header("Drunk Wobble")]
    [SerializeField] private float drunkRoll = 8f;
    [SerializeField] private float drunkYawWobble = 4f;
    [SerializeField] private float drunkPitchWobble = 3f;
    [Tooltip("How many drinks can stack; each stack multiplies the wobble intensity.")]
    [SerializeField, Min(1)] private int maximumDrunkStacks = 3;

    private PlayerInputHandler input;
    private PlayerMotor motor;
    private TrainPassenger trainPassenger;
    private PlayerInventory inventory;
    private BodyCarrier bodyCarrier;
    private Weapon weapon;
    private LassoTool lasso;
    private Health health;
    private PlayerBody body;
    private Animator animator;
    private float yaw;
    private float pitch;
    private float smoothedYaw;
    private float smoothedPitch;
    private float currentFov;
    private Transform cameraDefaultParent;
    private Vector3 cameraDefaultLocalPosition;
    private Quaternion cameraDefaultLocalRotation;
    private float throwChargeStartedAt;
    private bool isChargingThrow;
    private bool isChargingBodyThrow;
    private bool isConsuming;
    private float consumeStartedAt;
    private LootDataSO consumingItem;
    private bool isChargingLasso;
    private float lassoChargeStartedAt;
    private ItemHolder itemHolder;
    private NetworkPlayer networkPlayer;
    private float drunkEndTime;
    private float drunkTotalDuration = 1f;
    private int drunkStacks;

    private bool IsLocalPlayer => networkPlayer == null || networkPlayer == NetworkPlayer.Local;

    private void Awake()
    {
        networkPlayer = GetComponent<NetworkPlayer>();
        input = GetComponent<PlayerInputHandler>();
        motor = GetComponent<PlayerMotor>();
        trainPassenger = GetComponent<TrainPassenger>();
        inventory = GetComponent<PlayerInventory>();
        bodyCarrier = GetComponent<BodyCarrier>();
        itemHolder = GetComponent<ItemHolder>();
        weapon = itemHolder != null ? itemHolder.RuntimeWeapon : GetComponentInChildren<Weapon>(true);
        lasso = GetComponent<LassoTool>();
        health = GetComponent<Health>();
        body = GetComponent<PlayerBody>();
        animator = GetComponentInChildren<Animator>();
        activePlayers.Add(this);

        if (interactUI == null)
            interactUI = FindFirstObjectByType<InteractUI>();

        if (health != null)
        {
            health.OnHit += OnHit;
            health.OnDeath += OnDeath;
        }

        if (inventory != null)
            inventory.OnItemConsumed += HandleItemConsumed;

        if (cameraTransform == null)
            cameraTransform = Camera.main != null ? Camera.main.transform : null;

        if (mainCamera == null)
            mainCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : Camera.main;

        yaw = transform.eulerAngles.y;
        pitch = cameraTransform != null ? NormalizeAngle(cameraTransform.localEulerAngles.x) : 0f;
        smoothedYaw = yaw;
        smoothedPitch = pitch;
        currentFov = mainCamera != null ? mainCamera.fieldOfView : normalFov;

        if (cameraTransform != null)
        {
            cameraDefaultParent = cameraTransform.parent;
            cameraDefaultLocalPosition = cameraTransform.localPosition;
            cameraDefaultLocalRotation = cameraTransform.localRotation;
        }

        if (mainCamera != null)
            mainCamera.fieldOfView = currentFov;
        else
            Debug.LogWarning("PlayerController: No camera found for look/zoom.");
    }

    public void SetInteractUI(InteractUI ui)
    {
        interactUI = ui;
    }

    public Transform CameraTransform => cameraTransform;
    public Camera ViewCamera => mainCamera;

    public void SetOutputCamera(Camera outputCamera)
    {
        if (outputCamera == null)
            return;

        mainCamera = outputCamera;
        currentFov = mainCamera.fieldOfView;
    }

    private void Update()
    {
        if (IsKnockedDown)
            return;

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
        if (IsKnockedDown)
            return;

        ApplyTrainMotion();
        ApplyLookRotation();
    }

    public bool IsKnockedDown { get; private set; }

    // Used by PlayerKnockdown on the owning client: the ragdoll drives the body, the camera rides the head.
    public void SetKnockedDown(bool knockedDown)
    {
        if (!IsLocalPlayer || IsKnockedDown == knockedDown)
            return;

        IsKnockedDown = knockedDown;

        if (knockedDown)
        {
            CancelThrow();
            AttachCameraToRagdoll();
            return;
        }

        RestoreCamera();
    }

    private void AttachCameraToRagdoll()
    {
        if (cameraTransform != null && ragdollCameraAnchor != null)
            cameraTransform.SetParent(ragdollCameraAnchor, true);
    }

    private void RestoreCamera()
    {
        if (cameraTransform != null && cameraDefaultParent != null)
        {
            cameraTransform.SetParent(cameraDefaultParent, false);
            cameraTransform.localPosition = cameraDefaultLocalPosition;
            cameraTransform.localRotation = cameraDefaultLocalRotation;
        }

        yaw = transform.eulerAngles.y;
        pitch = 0f;
        smoothedYaw = yaw;
        smoothedPitch = pitch;
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

        float drunk = GetDrunkStrength();
        float yawWobble = Mathf.Sin(Time.time * 0.9f) * drunkYawWobble * drunk;
        float pitchWobble = Mathf.Sin(Time.time * 1.7f) * drunkPitchWobble * drunk;
        float roll = Mathf.Sin(Time.time * 1.3f) * drunkRoll * drunk;

        transform.rotation = Quaternion.Euler(0f, smoothedYaw + yawWobble, 0f);

        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(smoothedPitch + pitchWobble, 0f, roll);
    }

    // Drinks stack: each one adds its full duration on top of what is left and deepens the wobble, up to a cap.
    public void ApplyDrunk(float duration)
    {
        float remaining = Mathf.Max(drunkEndTime - Time.time, 0f);
        drunkStacks = remaining > 0f ? Mathf.Min(drunkStacks + 1, maximumDrunkStacks) : 1;
        drunkEndTime = Time.time + remaining + duration;
        drunkTotalDuration = Mathf.Max(remaining + duration, 0.01f);
    }

    // Full strength for most of the effect, easing out over its last third.
    private float GetDrunkStrength()
    {
        float remaining = drunkEndTime - Time.time;
        if (remaining <= 0f)
        {
            drunkStacks = 0;
            return 0f;
        }

        return Mathf.Clamp01(remaining / (drunkTotalDuration / 3f)) * Mathf.Max(drunkStacks, 1);
    }

    private void HandleItemConsumed(LootDataSO item)
    {
        if (health != null && item.DrinkHealPercent > 0f)
            health.Heal(health.MaxHealth * item.DrinkHealPercent);

        if (item.DrunkDuration <= 0f)
            return;

        if (IsLocalPlayer)
            ApplyDrunk(item.DrunkDuration);
        else if (TryGetComponent(out NetworkInventoryAuthority inventoryAuthority))
            inventoryAuthority.NotifyDrunkToOwner(item.DrunkDuration);
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

        bool sprinting = input.SprintHeld &&
            (inventory == null || inventory.ActiveItem == null || !inventory.ActiveItem.IsHeavy) &&
            (bodyCarrier == null || !bodyCarrier.IsCarryingBody);
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
        // Carrying a body occupies both hands: no inventory switching, only drop or charge-throw.
        if (bodyCarrier != null && bodyCarrier.IsCarryingBody)
        {
            HandleBodyThrowInput();
            return;
        }

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
        if (bodyCarrier != null && bodyCarrier.IsCarryingBody)
            return;

        if (inventory == null || inventory.ActiveItem == null)
            return;

        if (inventory.ActiveItem.IsConsumable)
        {
            HandleConsumeInput();
            return;
        }

        if (inventory.ActiveItem.ItemType == InventoryItemType.Tool)
        {
            HandleLassoInput();
            return;
        }

        CancelConsume();
        CancelLassoCharge();

        if (!inventory.ActiveItem.IsWeapon)
            return;

        weapon = itemHolder != null ? itemHolder.RuntimeWeapon : weapon;
        if (!inventory.TryUseActiveItem(weapon) || weapon == null)
            return;

        weapon.HandleInput(input.FirePressed, input.FireHeld, input.ReloadPressed);
    }

    private void HandleConsumeInput()
    {
        float useDuration = inventory.ActiveItem.UseDuration;
        if (useDuration <= 0f)
        {
            if (input.FirePressed && !isChargingThrow)
                inventory.TryConsumeActive();
            return;
        }

        if (input.FirePressed && !isChargingThrow && !isConsuming)
        {
            isConsuming = true;
            consumeStartedAt = Time.time;
            consumingItem = inventory.ActiveItem;
            interactUI?.StartHeldProgress(useDuration, "Drinking...");
            return;
        }

        if (!isConsuming)
            return;

        // Swapping the item mid-drink must not consume whatever ended up in hand.
        if (!input.FireHeld || isChargingThrow || inventory.ActiveItem != consumingItem)
        {
            CancelConsume();
            return;
        }

        if (Time.time - consumeStartedAt < useDuration)
            return;

        isConsuming = false;
        consumingItem = null;
        interactUI?.FinishProgress();
        inventory.TryConsumeActive();
    }

    private void CancelConsume()
    {
        if (!isConsuming)
            return;

        isConsuming = false;
        consumingItem = null;
        interactUI?.CancelProgress();
    }

    private void HandleLassoInput()
    {
        if (lasso == null)
            return;

        if (input.FirePressed && !isChargingThrow && !isChargingLasso)
        {
            isChargingLasso = true;
            lassoChargeStartedAt = Time.time;
            interactUI?.StartHeldProgress(lasso.ChargeDuration, "Lasso...");
            return;
        }

        if (!isChargingLasso)
            return;

        if (isChargingThrow)
        {
            CancelLassoCharge();
            return;
        }

        if (!input.FireReleased && input.FireHeld)
            return;

        float charge = Mathf.Clamp01((Time.time - lassoChargeStartedAt) /
            Mathf.Max(lasso.ChargeDuration, 0.01f));
        isChargingLasso = false;
        interactUI?.FinishProgress();
        lasso.TryThrow(GetAimRay(), charge);
    }

    private void CancelLassoCharge()
    {
        if (!isChargingLasso)
            return;

        isChargingLasso = false;
        interactUI?.CancelProgress();
    }

    private Ray GetAimRay()
    {
        if (mainCamera != null)
            return mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));

        Transform origin = cameraTransform != null ? cameraTransform : transform;
        return new Ray(origin.position, origin.forward);
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

    private void HandleBodyThrowInput()
    {
        if (input.DropPressed)
        {
            isChargingBodyThrow = true;
            throwChargeStartedAt = Time.time;
            interactUI?.StartHeldProgress(throwChargeDuration, "Throwing body...");
            return;
        }

        if (isChargingBodyThrow && input.DropReleased)
            ReleaseBodyThrow();
    }

    private void ReleaseBodyThrow()
    {
        float charge = Mathf.Clamp01((Time.time - throwChargeStartedAt) / Mathf.Max(throwChargeDuration, 0.01f));
        float force = Mathf.Lerp(minimumThrowForce, maximumThrowForce, charge);
        Vector3 throwDirection = mainCamera != null ? mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f)).direction : transform.forward;
        isChargingBodyThrow = false;
        interactUI?.FinishProgress();
        bodyCarrier.Throw(throwDirection * force);
    }

    private void OnDestroy()
    {
        activePlayers.Remove(this);
        if (health != null)
        {
            health.OnHit -= OnHit;
            health.OnDeath -= OnDeath;
        }

        if (inventory != null)
            inventory.OnItemConsumed -= HandleItemConsumed;
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

        if (!IsLocalPlayer)
            return;

        // Keep cameraTransform parented under the head bone: disabling this component (below) stops
        // us from overriding its rotation every LateUpdate, so it's free to ride the ragdoll physics.
        AttachCameraToRagdoll();

        if (DeathCameraController.Instance != null)
            DeathCameraController.Instance.NotifyPlayerDied(this);
        else
            Debug.LogWarning("PlayerController: local player died but there is no DeathCameraController in the scene.", this);

        enabled = false;
    }

    // After a teleport, otherwise the next LateUpdate snaps the body back to the old heading.
    public void SyncLookToTransform()
    {
        yaw = transform.eulerAngles.y;
        smoothedYaw = yaw;
    }

    // Called by PlayerBody once its Health has been revived; hands control back to the player.
    public void Revive()
    {
        if (!IsLocalPlayer)
            return;

        IsKnockedDown = false;
        RestoreCamera();

        DeathCameraController.Instance?.Deactivate();

        enabled = true;

        if (input != null)
            input.enabled = true;

        if (motor != null)
            motor.enabled = true;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}
