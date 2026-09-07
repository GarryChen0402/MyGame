using UnityEngine;

// Player hurt channel (Forge LivingHurt-style, mirrors MobEntityHurtEvent):
// fired pre-damage - Player's static main handler runs the shared
// LivingEntity.Hurt flow synchronously on publish, so Before/After
// subscribers see the attack around the application. ZombieAttackState is the
// v1 publisher; Player.Hurt stays public for the handler and future direct
// non-actor sources.
public class PlayerHurtEvent : GameEvent
{
    public Player player;       // the player receiving the attack (type-guaranteed victim)
    public Entity attacker;     // null = non-actor damage (future fall/fire sources)
    public float amount;
    public Vector3 knockbackVelocity;
}

// Published from Player.Dead with the lethal hit's attacker; MobAI subscribes
// to release its chase target lock (design doc §2.1). Player death keeps no
// corpse: Dead then runs the v1 full-HP reset in place.
public class PlayerDeadEvent : GameEvent
{
    public Entity attacker;     // the lethal hit's attacker; null on non-actor death
}
