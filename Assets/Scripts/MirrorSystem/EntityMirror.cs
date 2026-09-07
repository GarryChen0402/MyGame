using UnityEngine;

// Pure-data mirror of an entity's render-read state (rule R-C2-1): the only
// writer is the MirrorSync sync point (EntityBinding), the only readers are
// render shells. Carries no logic reference - the shell table keys on
// EntityId instead. Field set = the former per-frame shell read surface
// (MobVisualSync/EntityRenderer), see design C-2 §4.1.
public class EntityMirror
{
    public readonly int EntityId;

    public bool IsDead;                  // LivingEntity source; non-living stays false
    public Vector3 PrevPosition;
    public Vector3 Position;
    public float BoxMinY;                // MainBox (AABBs[0]) y range - shell lift formulas
    public float BoxMaxY;
    public float PrevPitch, Pitch;
    public float PrevYaw, Yaw;
    public float PrevHeadYaw;            // mob data layer; defaults on items
    public float HeadYaw;
    public bool HeadLocked;
    public float InvincibleTimer;
    public Vector2 MotionXZ;             // Motion horizontal component (walk gate)

    public EntityMirror(int entityId) => EntityId = entityId;

    // Tick-sync copy (rule R-C1-0: mirror content is fully determined by the
    // logic state; no-tick frames copy the same values). Runs from the
    // MirrorSync binding - never called from a shell.
    public void ApplyFrom(Entity src)
    {
        if(src == null)return;
        PrevPosition = src.PrevPosition;
        Position = src.Position;
        if(src.AABBs != null && src.AABBs.Count > 0)
        {
            BoxMinY = src.MainBox.MinRange.y;
            BoxMaxY = src.MainBox.MaxRange.y;
        }
        PrevPitch = src.PrevPitch;
        Pitch = src.pitch;
        PrevYaw = src.PrevYaw;
        Yaw = src.yaw;
        InvincibleTimer = src.InvincibleTimer;
        MotionXZ = new Vector2(src.Motion.x, src.Motion.z);
        if(src is LivingEntity living)IsDead = living.IsDead;
        if(src is MobEntity mob)
        {
            HeadYaw = mob.HeadYaw;
            PrevHeadYaw = mob.PrevHeadYaw;
            HeadLocked = mob.HeadLocked;
        }
    }
}
