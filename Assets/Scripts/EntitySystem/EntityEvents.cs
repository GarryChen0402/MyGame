
using UnityEngine;

public class SummonEntityEvent : GameEvent
{
    public Entity entity;
}

public class DestroyEntity : GameEvent
{
    public Entity entity;
}

// Render shell spawn/despawn (rule R-C2-3): published by the logic spawn
// entries once the entity's data is complete - SummonEntityEvent fires too
// early (in the Entity ctor, before Stack/Definition exist) and stays the
// logic-registration channel. Payload is pure DTO data + the entity mirror;
// no logic reference reaches the shell manager.
public class EntityShellSpawnEvent : GameEvent
{
    public int entityId;
    public bool isItem;
    public ushort itemId;    // isItem: shell mesh route (def.IsBlockItem)
    public string modelId;   // mob: EntityModels resource lookup
    public EntityMirror mirror;
}

public class EntityShellDespawnEvent : GameEvent
{
    public int entityId;
}

// Hurt/damage channel (Forge LivingHurt-style): fired pre-damage - MobEntity's
// main handler runs the existing hurt flow synchronously on publish. Player-side
// damage publishes no hurt event until the player hurt eventification task.
public class MobEntityHurtEvent : GameEvent
{
    public MobEntity entity;    // the mob receiving the attack (type-guaranteed victim)
    public Entity attacker;     // null = non-actor damage (future fall/fire sources)
    public float amount;
    public Vector3 knockbackVelocity;
}

// Published at the lethal hit (inside the hurt flow), pre-death-transition:
// MobEntity's main handler runs the Dead() state transition from it. Carries
// the killer for loot Operator etc.
public class MobEntityDeathEvent : GameEvent
{
    public MobEntity entity;
    public Entity attacker;     // the lethal hit's attacker; null on non-actor death
}