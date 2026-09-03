using UnityEngine;

// Pushed to the handler stack while a panel UI is open: releases the cursor
// for clicking and closes the panel on Escape (UIManager.CloseUI pops it back
// off, after which the player handler below re-locks the cursor).
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
        if(Input.GetKeyDown(KeyCode.Escape))
            UIManager.Instance.CloseUI();
    }
}
