using System.Collections.Generic;
using UnityEngine;

// Runs before the default-order logic tick (GameLoopDriver -> WorldManager ->
// EntityManager.Update): the active handler writes the player's target speed
// into Entity.Motion here so the same frame's TickPhysics consumes it - a
// later order would lag every movement one frame behind the input.
[DefaultExecutionOrder(-100)]
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
        // L1 gate (Part B §5.4): during stepper-driven startup the handler
        // registries are still empty when this scene's Awake runs, so the
        // push waits for the completion event. The direct path (domain-
        // reload-off re-play, registries live) pushes immediately.
        if(GameBootstrap.IsBootstrapped)TryPushDefault();
        else EventBus.Instance.Subscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
    }

    private void OnBootstrapCompleted(BootstrapCompletedEvent evt)
    {
        EventBus.Instance.Unsubscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
        TryPushDefault();
    }

    private void TryPushDefault()
    {
        TryPush("minecraft:player_input_handler");
    }

    private Stack<IInputHandler> inputHandlers = new();
    // Empty stack returns null instead of throwing: between this Awake and
    // bootstrap completion no handler exists yet, and Update polls this every
    // frame (also covers the failure stance, where the push never comes).
    public IInputHandler CurrentInputHandler => inputHandlers.Count > 0 ? inputHandlers.Peek() : null;


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