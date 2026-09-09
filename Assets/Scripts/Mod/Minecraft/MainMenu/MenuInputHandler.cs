using UnityEngine;

// Menu input context (design §3.4): pushed over the player handler while the
// main menu is open. The menu is pure mouse UI - no key binds are consumed.
// Input routing only feeds the stack top, so an empty OnUpdate shields the
// player handler underneath from E/ESC/movement without per-key interception;
// the cursor is the only thing this context owns.
public class MenuInputHandler : IInputHandler
{
    public MenuInputHandler()
    {
        modId = "minecraft";
        name = "menu_input_handler";
    }

    public override void OnEnter()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public override void OnUpdate() {}

    // Convention shared with PlayerInputHandler: leaving the context re-locks
    // (the actual lock after a pop comes from the underlying handler's OnEnter).
    public override void OnExit()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }
}
