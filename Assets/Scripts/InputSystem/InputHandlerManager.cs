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


    public void Push(IInputHandler input)
        => inputHandlers.Push(input);
    
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
        var top = inputHandlers.Pop();
        top.OnExit();
        CurrentInputHandler.OnEnter();
        return true;
    }

    private void Update()
    {
        CurrentInputHandler?.OnUpdate();
    }
}