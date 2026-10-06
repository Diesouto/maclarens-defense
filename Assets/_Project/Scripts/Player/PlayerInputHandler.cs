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
    public bool FireReleased => input.Player.Fire.WasReleasedThisFrame();
    public bool ReloadPressed => input.Player.Reload.WasPressedThisFrame();
    public bool InteractPressed => input.Player.Interact.WasPressedThisFrame();
    public bool InteractHeld => input.Player.Interact.IsPressed();
    public bool CrouchHeld => input.Player.Crouch.IsPressed();
    public bool CrouchPressed => input.Player.Crouch.WasPressedThisFrame();
    public bool DropPressed => input.Player.Drop.WasPressedThisFrame();
    public bool DropHeld => input.Player.Drop.IsPressed();
    public bool DropReleased => input.Player.Drop.WasReleasedThisFrame();
    public bool InventoryUpPressed => input.Player.InventoryUp.WasPressedThisFrame();
    public bool InventoryDownPressed => input.Player.InventoryDown.WasPressedThisFrame();
    public bool Number1Pressed => Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame;
    public bool Number2Pressed => Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame;
    public bool Number3Pressed => Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame;
    public bool Number4Pressed => Keyboard.current != null && Keyboard.current.digit4Key.wasPressedThisFrame;

    private void Awake()
    {
        input = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        input.Enable();
        LockCursor();
    }

    private void OnDisable()
    {
        input.Disable();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && isActiveAndEnabled)
            LockCursor();
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
