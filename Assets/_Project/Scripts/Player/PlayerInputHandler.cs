using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    private InputSystem_Actions input;

    public Vector2 Move => input.Player.Move.ReadValue<Vector2>();
    public Vector2 Look => input.Player.Look.ReadValue<Vector2>();

    public bool SprintHeld => input.Player.Sprint.IsPressed();
    public bool SprintPressed => input.Player.Sprint.WasPressedThisFrame();
    public bool JumpPressed => input.Player.Jump.WasPressedThisFrame();
    
    public bool AimHeld => input.Player.Aim.IsPressed();
    public bool AimPressed => input.Player.Aim.WasPressedThisFrame();
    public bool FireHeld => input.Player.Fire.IsPressed();
    public bool FirePressed => input.Player.Fire.WasPressedThisFrame();
    public bool ReloadPressed => input.Player.Reload.WasPressedThisFrame();
    public bool InteractPressed => input.Player.Interact.WasPressedThisFrame();
    public bool CrouchHeld => input.Player.Crouch.IsPressed();
    public bool CrouchPressed => input.Player.Crouch.WasPressedThisFrame();

    private void Awake()
    {
        input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        input.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        input.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}