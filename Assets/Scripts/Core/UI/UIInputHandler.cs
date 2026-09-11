using UnityEngine;

// Pushed to the handler stack while a panel UI is open: releases the cursor
// for clicking and closes the panel on Escape (UIManager.CloseUI pops it back
// off, after which the player handler below re-locks the cursor). The E-shaped
// close is the close_ui action, carried by the UI action ring from P6
// (UIManager polls it; the whitelist above still gates the context, so
// pressing E with no panel open does nothing).
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
        // Esc is a system key (never in the binding table).
        if(Input.GetKeyDown(KeyCode.Escape))
            UIManager.Instance.CloseUI();
    }
}
