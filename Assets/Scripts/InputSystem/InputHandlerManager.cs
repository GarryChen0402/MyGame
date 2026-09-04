using System.Collections.Generic;
using UnityEngine;

public class InputHandlerManager : MonoBehaviour
{
    private static InputHandlerManager instance = null;
    public static InputHandlerManager Instance => instance;

    // private InputHandlerManager()
    // {

    // }
    private void Awake()
    {
        if(instance == null)instance = this;
        else Destroy(gameObject);
        TryPush("minecraft:player_input_handler");
    }

    private Stack<IInputHandler> inputHandlers = new();
    public IInputHandler CurrentInputHandler => inputHandlers.Peek();


    // Context switch: clicks routed to the outgoing context are stale for the
    // incoming one, so the action edge buffer is drained on every switch
    // (MC's KeyMapping.releaseAll when a Screen opens/closes).
    public void Push(IInputHandler input)
    {
        KeyBindingManager.Instance.ClearPendingClicks();
        inputHandlers.Push(input);
    }
    
    public bool TryPush(string inputHandlerId)
    {
        if(string.IsNullOrEmpty(inputHandlerId))return false;
        if(!ResourceSystem.Instance.InputHandlers.TryGetResourceWithFullName(inputHandlerId, out var handler))return false;
        if(inputHandlers.Count > 0)CurrentInputHandler.OnExit();
        Push(handler);
        handler.OnEnter();
        return true;
    }

    public bool Pop()
    {
        if(inputHandlers.Count == 1)return false;
        KeyBindingManager.Instance.ClearPendingClicks();
        var top = inputHandlers.Pop();
        top.OnExit();
        CurrentInputHandler.OnEnter();
        return true;
    }

    private void Update()
    {
        // The keybinding layer refreshes here, ahead of the active handler, so
        // edge presses of the current frame are routed and ready to consume.
        KeyBindingManager.Instance.RefreshInput();
        CurrentInputHandler?.OnUpdate();
    }
}