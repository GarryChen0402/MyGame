// v1 zombie brain assembly (design doc §2): builds the root machine's four
// top states and its declarative transition table in registration order.
// Wired from MobEntity.Init via a ModelId branch (design doc §2.3); the
// AIDefinition resource round replaces the branch, this wiring stays.
public static class ZombieAI
{
    public static void Configure(MobEntity mob)
    {
        var def = mob.Definition;
        var root = mob.AI.RootMachine;
        var ctx = mob.AI.Context;

        var wander = new AIStateMachine();
        var wanderStop = new ZombieWanderStopState();
        var wanderMove = new ZombieWanderMoveState();
        wander.AddState("Stop", wanderStop);
        wander.AddState("Move", wanderMove);
        wander.SetDefaultState("Stop");                       // wander always re-enters standing (design doc §3.1)
        wander.AddTransition("Stop", "Move", () => wanderStop.DurationExpired);
        wander.AddTransition("Move", "Stop", () => wanderMove.DurationExpired || mob.HorizontalBlocked);   // duration over, or wall bump

        var chase = new ZombieChaseState();
        var attack = new ZombieAttackState();

        root.AddState("Wander", wander);
        root.AddState("Chase", chase);
        root.AddState("Attack", attack);
        root.AddState("Dead", new ZombieDeadState());
        root.SetDefaultState("Wander");

        // Registration order is evaluation order; first hit wins. Attack's
        // entries are ordered so a lost target drops to Wander, never Chase,
        // on the same tick. All distances read the sense-cached
        // Context.TargetDistance (center-to-center, design doc §2.2).
        root.AddTransition("Wander", "Chase", () => ctx.Target != null && ctx.TargetDistance < def.SenseRange);
        root.AddTransition("Chase", "Wander", () => ctx.Target == null || ctx.TargetDistance >= def.ChaseRange);
        root.AddTransition("Chase", "Attack", () => ctx.Target != null && ctx.TargetDistance < def.AttackRange);
        root.AddTransition("Attack", "Wander", () => ctx.Target == null);
        root.AddTransition("Attack", "Chase", () => ctx.Target != null && ctx.TargetDistance >= def.AttackRange);
        // Expression-only registration: IsDead short-circuits the AI before the
        // machine ticks, so these are runtime-unreachable - they reserve the
        // path for future AI-caused deaths (self-harm, etc.).
        root.AddTransition("Wander", "Dead", () => mob.IsDead);
        root.AddTransition("Chase", "Dead", () => mob.IsDead);
        root.AddTransition("Attack", "Dead", () => mob.IsDead);
    }
}
