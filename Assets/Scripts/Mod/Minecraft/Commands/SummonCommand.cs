using UnityEngine;

// /summon settlement (design §5.3, P2): domain resolution (entity name ->
// definition) lives here in Execute like /give; the spawn itself reuses the
// logic-side EntityManager entry (mirror registration + shell spawn event).
// Optional coordinates are Nullable<float> - absent components default to the
// player's current position component by component.
[Command("minecraft:summon")]
public class SummonCommand : CommandBase
{
    [Param] public string entity;
    [Param] public float? x;
    [Param] public float? y;
    [Param] public float? z;

    public override void Execute(CommandContext ctx)
    {
        // Short names resolve in the minecraft: namespace, like the command name.
        string fullName = entity.IndexOf(':') >= 0 ? entity : "minecraft:" + entity;
        Vector3 playerPos = Player.Instance.Position;
        var pos = new Vector3(x ?? playerPos.x, y ?? playerPos.y, z ?? playerPos.z);
        if(!EntityManager.SummonMobEntity(pos, fullName))
        {
            ctx.Error($"Unknown entity: {entity}");
            return;
        }
        ctx.Reply($"Summoned {fullName} at {pos.x:F1} {pos.y:F1} {pos.z:F1}");
    }
}
