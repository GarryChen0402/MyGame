// Death expression state (design doc §2.3/§3.4). The data layer owns death -
// IsDead short-circuits the AI tick and the 1.5s corpse timer removes the
// body - so this state is runtime-unreachable in v1. It is registered for
// semantic completeness and for future AI-caused deaths (self-harm etc.).
public class ZombieDeadState : AIState
{
    public override void Tick(float dt) { }
}
