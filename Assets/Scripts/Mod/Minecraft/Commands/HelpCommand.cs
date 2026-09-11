using System.Text;

// /help (D5 of Docs/指令系统-设计草案.md §5.3): no argument lists every
// registered command with its auto-generated usage; with an argument shows
// the single command's usage line.
[Command("minecraft:help")]
public class HelpCommand : CommandBase
{
    [Param] public string cmd = "";

    public override void Execute(CommandContext ctx)
    {
        var commands = ResourceSystem.Instance.Commands;
        if(string.IsNullOrEmpty(cmd))
        {
            var sb = new StringBuilder($"Available commands ({commands.Count})");
            // Number ids = registration order = full-name sort (scanner sorts).
            for(ushort i = 0; i < commands.Count; i++)
            {
                if(!commands.TryGetResourceWithNumberId(i, out var command))continue;
                sb.Append('\n').Append(command.Usage ?? command.FullName);
            }
            ctx.Reply(sb.ToString());
            return;
        }
        string fullName = cmd.IndexOf(':') >= 0 ? cmd : "minecraft:" + cmd;
        if(!commands.TryGetResourceWithFullName(fullName, out var target))
        {
            ctx.Error($"Unknown command: {cmd} (see /help)");
            return;
        }
        ctx.Reply(target.Usage ?? target.FullName);
    }
}
