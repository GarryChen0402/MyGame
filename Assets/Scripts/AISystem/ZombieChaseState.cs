// Pursuit state (design doc §3.2): moves through the shared A* follower. The
// transition table owns entry/exit - this state only keeps the approach move
// running, and Attack uses the same one-liner so a switch never restarts the
// path or its cursor.
public class ZombieChaseState : AIState
{
    public override void Tick(float dt)
        => Brain.Path.Follow(Brain.Context, Brain.Intent, dt);   // A* follow (design doc §4)
}
