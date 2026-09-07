using System;
using System.Collections.Generic;
using UnityEngine;

// Shared physiology of biological entities (design doc Docs/Buff系统-规则设计.md
// §3.2, plan B): health, hurt, invincibility window, heal and the death entry
// live here so MobEntity and Player stop duplicating them. Subclasses keep
// their own channels - MobEntity receives damage eventified (MobEntityHurtEvent)
// and broadcasts death, Player is hurt through PlayerHurtEvent and resets in
// place - so Hurt and Dead are virtual entry points over one shared flow.
// BuffList also lives here (rule §3.2): mount/remove run as event main
// handlers, ticking rides the shared OnUpdate chain (rule §4 tick point).
public class LivingEntity : Entity
{
    public ValueEntry MaxHealth {get; protected set;}
    public float CurrentHealth {get; protected set;}
    public bool IsDead {get; protected set;} = false;

    // Last TickPhysics ground result (Entity.OnGround), exposed so AI jump
    // gating and the input layer share one shape.
    public bool IsOnGround => OnGround;

    // ---- BuffList (design doc Docs/Buff系统-代码设计.md §4/§5/§6) ----

    private readonly List<BuffInstance> buffs = new();
    public IReadOnlyList<BuffInstance> Buffs => buffs;   // debug / UI read view

    // Add/remove event main handlers, registered once in the static ctor:
    // mount and removal run synchronously on publish - publishers fire
    // requests, handlers apply. Registration always precedes any publish:
    // any LivingEntity instance construction triggers this static ctor first.
    private static readonly Action<BuffAddEvent> buffAddHandler = OnBuffAdd;
    private static readonly Action<BuffRemoveEvent> buffRemoveHandler = OnBuffRemove;

    static LivingEntity()
    {
        EventBus.Instance.Subscribe(buffAddHandler);
        EventBus.Instance.Subscribe(buffRemoveHandler);
    }

    // Mount main handler (rule §3.5 flow): identity check first - key
    // (Definition, Source, variant) all equal -> time merge (rule §4);
    // otherwise the factory builds a fresh instance that the handler
    // completes and mounts, then OnApply fires.
    private static void OnBuffAdd(BuffAddEvent evt)
    {
        var target = evt.Target;
        if(target.IsDead)return;          // corpse gate (players never set IsDead)
        var inst = evt.Definition.CreateInstance(evt.ParamsJson);   // pure parse, no side effects
        foreach(var existing in target.buffs)
            if(existing.Definition == evt.Definition
                && existing.Source == evt.Source
                && existing.VariantKey == inst.VariantKey)
            {
                existing.Timer += inst.Timer;   // identity match -> time merge; no re-apply (rule §4)
                evt.Result = true;
                return;
            }
        inst.Definition = evt.Definition;   // completed by the mount handler (rule §3.5)
        inst.Target = target;
        inst.Source = evt.Source;
        target.buffs.Add(inst);
        evt.Definition.OnApply(inst);
        evt.Result = true;
    }

    // Remove main handler (rule §3.6 single channel): matches Instance ->
    // exact, else Definition -> all stacked variants/sources of it, else ->
    // all. Detach happens before OnRemove so a callback never sees itself
    // mounted and an exception inside OnRemove cannot leave a corpse entry.
    private static void OnBuffRemove(BuffRemoveEvent evt)
    {
        var list = evt.Target.buffs;
        var toRemove = new List<BuffInstance>();
        foreach(var b in list)
            if(evt.Instance != null ? b == evt.Instance
                : evt.Definition == null || b.Definition == evt.Definition)
                toRemove.Add(b);
        foreach(var inst in toRemove)
        {
            if(!list.Remove(inst))continue;   // idempotent: already detached (re-entrant removal)
            inst.Definition.OnRemove(inst);
        }
    }

    // Applier entry (rule §3.5): publishes the add request - the main handler
    // mounts (or time-merges) synchronously; Result reports the outcome.
    public bool TryAddBuff(BuffDefinition def, string paramsJson = "{}", Entity source = null)
    {
        var evt = new BuffAddEvent { Target = this, Definition = def, ParamsJson = paramsJson, Source = source };
        EventBus.Instance.Publish(evt);
        return evt.Result;
    }

    // Active clearing (rule §3.6 removal path 1): by definition (all stacked
    // variants/sources) or everything when def is null. All removals travel
    // the single BuffRemoveEvent channel - no direct-call removal exists.
    public void RemoveBuffs(BuffRemoveReason reason = BuffRemoveReason.Active, BuffDefinition def = null)
        => EventBus.Instance.Publish(new BuffRemoveEvent { Target = this, Reason = reason, Definition = def });

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
        TickBuffs(deltaTime);
    }

    // Frame driver (rule §4 tick point + appendix A.3): decrements timers and
    // advances OnTick; expiring instances publish after the sweep, one event
    // each - never mutate the list while sweeping. Timers above the infinite
    // threshold stay frozen while OnTick keeps running (rule §3.6).
    private void TickBuffs(float dt)
    {
        if(buffs.Count == 0)return;
        var expired = new List<BuffInstance>();
        // Snapshot loop: OnTick may remove itself mid-iteration (remove events
        // are synchronous) - the snapshot keeps the sweep safe.
        foreach(var inst in buffs.ToArray())
        {
            var def = inst.Definition;
            if(inst.Timer > BuffInstance.InfiniteSeconds)
            {
                def.OnTick(inst, dt);   // infinite domain: time frozen, tick runs
                continue;
            }
            inst.Timer -= dt;
            if(inst.Timer <= 0f) { expired.Add(inst); continue; }   // expiring frame: no last OnTick
            def.OnTick(inst, dt);
        }
        foreach(var inst in expired)
            EventBus.Instance.Publish(new BuffRemoveEvent { Target = this, Reason = BuffRemoveReason.Expired, Instance = inst });
    }

    // Generic hurt flow (Hurt/Dead entry both virtual so subclass channels
    // stay their own): dead/invincibility gates -> damage -> hurt window ->
    // horizontal knockback write (vertical motion kept) -> death dispatch at
    // zero health. The hit log sits before the dispatch so a lethal frame
    // prints the pre-reset health, matching both former implementations.
    public virtual void Hurt(Entity attacker, float damage, Vector3 knockbackVelocity = default)
    {
        if(IsDead)return;
        if(InvincibleTimer > 0)return;
        CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, MaxHealth.CurrentValue);
        InvincibleTimer = HurtInvincibleSeconds;
        if(knockbackVelocity != Vector3.zero)
            Motion = new Vector3(knockbackVelocity.x, Motion.y, knockbackVelocity.z);

        Debug.Log($"[{GetType().Name}] took {damage:F1} damage -> {CurrentHealth:F1}/{MaxHealth.CurrentValue:F1} health");
        if(CurrentHealth <= 0f)
        {
            Dead(attacker);            // subclass death channel (corpse state / player full reset)
            ClearBuffsOnDeath();       // death clears the list (rule §3.6/§4): per-instance Death events
        }
    }

    // Death sweep (design doc 代码设计 §6.1): per-instance Death removal
    // events, run after the subclass death channel so callbacks see the
    // corpse state (mob) or the reset frame (player - no death marker by
    // design, the Reason tells the callback). Death-gated adds are rejected
    // above by the IsDead check.
    private void ClearBuffsOnDeath()
    {
        foreach(var inst in buffs.ToArray())
            EventBus.Instance.Publish(new BuffRemoveEvent { Target = this, Reason = BuffRemoveReason.Death, Instance = inst });
    }

    public void Heal(float heal)
    {
        if(IsDead)return;
        CurrentHealth = Mathf.Clamp(CurrentHealth + heal, 0, MaxHealth.CurrentValue);
    }

    // Death entry, dispatched by Hurt at zero health with the lethal hit's
    // attacker. Subclasses run their own death channel from here.
    protected virtual void Dead(Entity attacker) { }
}
