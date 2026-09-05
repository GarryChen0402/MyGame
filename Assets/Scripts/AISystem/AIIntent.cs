using UnityEngine;

// Execution intent produced by decision states (design doc §2.3). MobAI.Apply
// consumes it every frame, so states and Apply communicate only through this
// struct-shaped contract. Step-2 fields (LookYaw, attack requests) append
// here without changing the state-machine <-> Apply contract.
public class AIIntent
{
    // Horizontal unit direction; zero = stand still. Speed is uniformly the
    // mob's BaseMoveSpeed.
    public Vector3 MoveDirection;

    // Vertical step-up request (path follower, design doc §4.2 step-up):
    // consumed by MobAI.Apply this frame as a ground jump impulse. MobAI.Update
    // clears it before the decision tick, so a frame with no request never
    // inherits a stale one.
    public bool JumpRequested;

    // Entity to face this tick (pursuit states set it each tick they run).
    // MobAI.Update clears it with the other one-frame intents and evolves
    // MobEntity.HeadYaw toward the target's horizontal angle while it is set.
    public Entity LookAt;
}
