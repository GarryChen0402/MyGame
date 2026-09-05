using UnityEngine;

// v1 zombie brain assembly (design doc §2): builds the root machine's four
// top states and its declarative transition table in registration order.
// Registered as the AIDefinition "minecraft:zombie" assembler - MobEntity.Init
// dispatches on def.AIDefinitionFullName, so no ModelId branch remains.
// Behavior numbers come from this assembler's Config (AIDefinition.ConfigJson).
public static class ZombieAI
{
    // Parameter contract of this assembler, serialized into
    // AIDefinition.ConfigJson (same shape as ProcessingWorkContainer.Config).
    [System.Serializable]
    public class Config
    {
        public float SenseRange, ChaseRange, AttackRange;   // distance bands (design doc §2.2)
        public float AttackInterval;                        // session length per swing
        public float TurnSpeed;                             // deg/s idle turn
        public float KnockbackStrength;                     // attack knockback speed
        public float WanderMin = 3f, WanderMax = 6f;        // wander window roll range
    }

    public static void Configure(MobEntity mob, AIDefinition def)
    {
        var cfg = JsonUtility.FromJson<Config>(def.ConfigJson);
        if(cfg == null)   // corrupt/missing ConfigJson: keep the empty-root no-op
        {
            Debug.LogWarning($"[MobAI] {def.FullName} has unparsable ConfigJson - empty-root no-op");
            return;
        }

        var root = mob.AI.RootMachine;
        var ctx = mob.AI.Context;

        var wanderStop = new ZombieWanderStopState(cfg.TurnSpeed, cfg.WanderMin, cfg.WanderMax);
        var wanderMove = new ZombieWanderMoveState(cfg.WanderMin, cfg.WanderMax);
        var wander = new AIStateMachine();
        wander.AddState("Stop", wanderStop);
        wander.AddState("Move", wanderMove);
        wander.SetDefaultState("Stop");                       // wander always re-enters standing (design doc §3.1)
        wander.AddTransition("Stop", "Move", () => wanderStop.DurationExpired);
        wander.AddTransition("Move", "Stop", () => wanderMove.DurationExpired || mob.HorizontalBlocked);   // duration over, or wall bump

        var chase = new ZombieChaseState();
        // Swing damage is a species capability (stays on the MobDefinition);
        // rhythm and knockback are behavior numbers from the Config.
        var attack = new ZombieAttackState(mob.Definition.BaseDamage, cfg.KnockbackStrength, cfg.AttackInterval);

        root.AddState("Wander", wander);
        root.AddState("Chase", chase);
        root.AddState("Attack", attack);
        root.AddState("Dead", new ZombieDeadState());
        root.SetDefaultState("Wander");

        // Registration order is evaluation order; first hit wins. Attack's
        // entries are ordered so a lost target drops to Wander, never Chase,
        // on the same tick. All distances read the sense-cached
        // Context.TargetDistance (center-to-center, design doc §2.2).
        root.AddTransition("Wander", "Chase", () => ctx.Target != null && ctx.TargetDistance < cfg.SenseRange);
        root.AddTransition("Chase", "Wander", () => ctx.Target == null || ctx.TargetDistance >= cfg.ChaseRange);
        root.AddTransition("Chase", "Attack", () => ctx.Target != null && ctx.TargetDistance < cfg.AttackRange);
        root.AddTransition("Attack", "Wander", () => ctx.Target == null);
        root.AddTransition("Attack", "Chase", () => ctx.Target != null && ctx.TargetDistance >= cfg.AttackRange);
        // Expression-only registration: IsDead short-circuits the AI before the
        // machine ticks, so these are runtime-unreachable - they reserve the
        // path for future AI-caused deaths (self-harm, etc.).
        root.AddTransition("Wander", "Dead", () => mob.IsDead);
        root.AddTransition("Chase", "Dead", () => mob.IsDead);
        root.AddTransition("Attack", "Dead", () => mob.IsDead);
    }
}
