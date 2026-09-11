using UnityEngine;

// Input context of the pause page (PauseMenuUI's InputHandlerId): releases the
// cursor for clicking and closes the page on Escape (UIManager.CloseUI pops it
// back off, after which the player handler below re-locks the cursor).
// Deliberately NOT the shared UIInputHandler: JEI's follow mode keys off the
// "ui_input_handler" context (JEIPanel.IsWorldPanelOpen), so the item browser
// never shows over the pause page; no other action whitelist admits this name
// either, so E/T/R and the rest of the world actions stay inert while paused.
public class PauseInputHandler : IInputHandler
{
    public PauseInputHandler()
    {
        modId = "minecraft";
        name = "pause_input_handler";
    }

    public override void OnUpdate()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if(Input.GetKeyDown(KeyCode.Escape))
            UIManager.Instance?.CloseUI();
    }
}
