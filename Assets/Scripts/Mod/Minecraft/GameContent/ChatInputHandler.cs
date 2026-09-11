using UnityEngine;

// Input context of the chat panel (ChatPanelUI's InputHandlerId): releases the
// cursor for typing and closes the panel on Escape, without submitting (Enter
// submits - handled by the panel itself). Deliberately NOT the shared
// UIInputHandler: JEI's follow mode keys off the "ui_input_handler" context
// (JEIPanel.IsWorldPanelOpen), so chatting never summons the item browser.
// No other action whitelist admits this name either, so E/T/R and the rest of
// the world actions stay inert while the chat context sits on top.
public class ChatInputHandler : IInputHandler
{
    public ChatInputHandler()
    {
        modId = "minecraft";
        name = "chat_input_handler";
    }

    public override void OnUpdate()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if(Input.GetKeyDown(KeyCode.Escape))
            UIManager.Instance?.CloseUI();
    }
}
