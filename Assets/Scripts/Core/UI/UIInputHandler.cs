using UnityEngine;

// Pushed to the handler stack while a panel UI is open: releases the cursor
// for clicking and closes the panel on Escape or the close_ui action
// (UIManager.CloseUI pops it back off, after which the player handler below
// re-locks the cursor).
public class UIInputHandler : IInputHandler
{
    public UIInputHandler()
    {
        modId = "minecraft";
        name = "ui_input_handler";
    }

    public override void OnUpdate()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // Esc is a system key (never in the binding table). The E-shaped close
        // is the close_ui action: routing only feeds it while this handler is
        // on top of the stack, so pressing E with no panel open does nothing.
        if(Input.GetKeyDown(KeyCode.Escape) || KeyBindingManager.Instance.WasPressed("minecraft:close_ui"))
            UIManager.Instance.CloseUI();
    }
}
