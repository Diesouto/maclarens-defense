using UnityEngine;

[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float lookSensitivity = 1f;
    [SerializeField] private float lookSmoothing = 10f;
    [SerializeField] private float maxLookPitch = 80f;
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float aimFov = 45f;
    [SerializeField] private float zoomSpeed = 40f;

    private PlayerInputHandler input;
    private PlayerMotor motor;
    private Health health;
    private float yaw;
    private float pitch;
    private float smoothedYaw;
    private float smoothedPitch;
    private float currentFov;

    private void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        motor = GetComponent<PlayerMotor>();
        health = GetComponent<Health>();

        if (health != null)
            health.OnDeath += OnDeath;

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

    private void Update()
    {
        HandleLook();
        HandleMovement();
        HandleAimZoom();

        if (input.JumpPressed)
            motor.Jump();
    }

    private void LateUpdate()
    {
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

        motor.Move(direction, input.SprintHeld);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnDeath -= OnDeath;
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
