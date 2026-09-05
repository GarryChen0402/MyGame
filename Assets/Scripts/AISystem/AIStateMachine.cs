using System;
using System.Collections.Generic;

// Composite state: a machine is itself a state, so machines nest into
// hierarchies (HFSM, design doc §2.2). To its parent a machine behaves like
// any other state: OnEnter descends into the default child, Tick evaluates
// the registered transitions of the active child then recurses into it,
// OnExit walks the active child chain back up. Transitions targeting a
// machine descend into its default child the same way.
public class AIStateMachine : AIState
{
    private readonly Dictionary<string, AIState> states = new();
    private readonly List<Transition> transitions = new();
    private string defaultStateName;
    private AIState activeChild;
    private MobAI brain;

    // Root machine only: injected once by MobAI.Create, shared across the tree
    // through the virtual Brain chain on AIState.
    public override MobAI Brain => brain;
    internal void SetBrain(MobAI brain) => this.brain = brain;

    private readonly struct Transition
    {
        public readonly string From;
        public readonly string To;
        public readonly Func<bool> Condition;

        public Transition(string from, string to, Func<bool> condition)
        {
            From = from;
            To = to;
            Condition = condition;
        }
    }

    public void AddState(string name, AIState state)
    {
        states[name] = state;
        state.Name = name;
        state.Machine = this;
    }

    public void SetDefaultState(string name) => defaultStateName = name;

    // Central declarative transition table: evaluated in registration order
    // every tick, first firing condition wins - state switches are only ever
    // driven by these conditions, never by per-frame arbitration.
    public void AddTransition(string from, string to, Func<bool> condition)
        => transitions.Add(new Transition(from, to, condition));

    public override void OnEnter()
    {
        EnterDefault();
    }

    public override void OnExit()
    {
        activeChild?.OnExit();
        activeChild = null;
    }

    public override void Tick(float dt)
    {
        if(activeChild == null)
        {
            // Root machine has no parent switch to enter it, so the default
            // child self-enters on the first tick once configured.
            if(!EnterDefault())return;
        }

        foreach(var t in transitions)
        {
            if(t.From != activeChild.Name)continue;
            if(t.Condition())
            {
                SwitchTo(t.To);
                break;   // one transition per tick, registration order wins
            }
        }
        activeChild.Tick(dt);
    }

    // False when no states are registered: an unconfigured machine is a
    // per-frame no-op, so mobs without AI wiring keep their current behavior.
    private bool EnterDefault()
    {
        if(string.IsNullOrEmpty(defaultStateName) || !states.TryGetValue(defaultStateName, out var state))
            return false;
        activeChild = state;
        state.OnEnter();
        return true;
    }

    private void SwitchTo(string to)
    {
        AIState next = states[to];
        activeChild.OnExit();   // a composite child exits its own subtree recursively
        activeChild = next;
        next.OnEnter();         // a composite target descends into its default child
    }
}
