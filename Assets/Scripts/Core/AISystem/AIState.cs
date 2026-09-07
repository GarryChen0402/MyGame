// Base of all decision states (design doc §2.2). A state reads AIContext
// through Brain and emits AIIntent through Tick; state transitions are owned
// and evaluated by the parent machine, never by the state itself.
public abstract class AIState
{
    // Set by the parent machine's AddState; keys the parent's transition table.
    public string Name { get; internal set; }

    // Direct parent machine, injected on AddState.
    public AIStateMachine Machine { get; internal set; }

    // Walks the machine chain up to the shared brain (context/intent access).
    public virtual MobAI Brain => Machine?.Brain;

    public virtual void OnEnter() { }
    public virtual void OnExit() { }

    // Read-only on Context; writes into Brain.Intent.
    public abstract void Tick(float dt);
}
