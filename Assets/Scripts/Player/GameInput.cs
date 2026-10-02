using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Programmatic Input System actions for Borrowed Time.
///
/// Built in code rather than from an .inputactions asset so there is no asset
/// GUID to keep in sync with the scene, and so the actions exist the moment the
/// first scene loads without a project-wide settings step.
///
/// Actions: Move (composite 2D), Confirm, Cancel, Menu.
///
/// activeInputHandler is deliberately left on "Both", so the legacy scenes that
/// still call Input.GetAxisRaw / Input.GetKey keep working. This class only
/// ever reads through the NEW API - do not mix in UnityEngine.Input here.
/// </summary>
public static class GameInput
{
    private static InputAction move;
    private static InputAction confirm;
    private static InputAction cancel;
    private static InputAction menu;
    private static bool ready;

    public static void Enable()
    {
        if (ready)
            return;
        Build();
        move.Enable();
        confirm.Enable();
        cancel.Enable();
        menu.Enable();
        ready = true;
    }

    public static void Disable()
    {
        if (!ready)
            return;
        move.Disable();
        confirm.Disable();
        cancel.Disable();
        menu.Disable();
        ready = false;
    }

    private static void Ensure()
    {
        if (!ready)
            Enable();
    }

    private static void Build()
    {
        // ------------------------------------------------------------- Move
        move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");

        // WASD
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        // Arrow keys
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        move.AddBinding("<Gamepad>/leftStick");
        move.AddBinding("<Gamepad>/dpad");

        // ----------------------------------------------------------- Confirm
        confirm = new InputAction("Confirm", InputActionType.Button);
        confirm.AddBinding("<Keyboard>/enter");
        confirm.AddBinding("<Keyboard>/space");
        confirm.AddBinding("<Gamepad>/buttonSouth");

        // ------------------------------------------------------------ Cancel
        cancel = new InputAction("Cancel", InputActionType.Button);
        cancel.AddBinding("<Keyboard>/escape");
        cancel.AddBinding("<Gamepad>/buttonEast");

        // -------------------------------------------------------------- Menu
        menu = new InputAction("Menu", InputActionType.Button);
        menu.AddBinding("<Keyboard>/tab");
        menu.AddBinding("<Gamepad>/start");
    }

    /// <summary>Normalised-ish 2D movement. May be zero.</summary>
    public static Vector2 Move
    {
        get
        {
            Ensure();
            Vector2 v = move.ReadValue<Vector2>();
            if (v.sqrMagnitude > 1f)
                v.Normalize();
            return v;
        }
    }

    public static bool ConfirmPressed { get { Ensure(); return confirm.WasPressedThisFrame(); } }
    public static bool CancelPressed { get { Ensure(); return cancel.WasPressedThisFrame(); } }
    public static bool MenuPressed { get { Ensure(); return menu.WasPressedThisFrame(); } }
}
