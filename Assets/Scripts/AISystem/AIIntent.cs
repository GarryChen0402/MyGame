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
}
