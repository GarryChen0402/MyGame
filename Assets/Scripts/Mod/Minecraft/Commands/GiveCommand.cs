// /give settlement (design §5.3): the parse plan only produces raw tokens -
// domain resolution (item name -> definition) lives here in Execute, with
// error echo; the grant reuses the container processor's settlement body.
[Command("minecraft:give")]
public class GiveCommand : CommandBase
{
    [Param] public string item;
    [Param] public int? amount = 1;

    public override void Execute(CommandContext ctx)
    {
        int count = amount ?? 1;
        if(count <= 0)
        {
            ctx.Error($"Invalid amount: {count} (expected a positive integer); usage: {Usage}");
            return;
        }
        // Short names resolve in the minecraft: namespace, like the command name.
        string fullName = item.IndexOf(':') >= 0 ? item : "minecraft:" + item;
        var rs = ResourceSystem.Instance;
        if(!rs.ItemDefinitions.TryGetResourceWithFullName(fullName, out var def))
        {
            ctx.Error($"Unknown item: {item}");
            return;
        }
        int granted = ContainerCommandProcessor.Instance.GiveItem(def.FullName, count);
        if(granted <= 0)
        {
            ctx.Error("Inventory full, nothing given");
            return;
        }
        if(granted < count)
        {
            ctx.Error($"Inventory full: only gave {granted}/{count} {def.FullName}");
            return;
        }
        ctx.Reply($"Gave {def.FullName} x{granted}");
    }
}
