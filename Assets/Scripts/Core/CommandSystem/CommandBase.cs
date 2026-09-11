using System;

// Command system core shape (D3-C of Docs/指令系统-设计草案.md): a class IS a
// command. [Command] carries the identity (single-assembly scan, so the full
// name must be explicit on the class); [Param]-marked public fields declare
// the positional arguments; the fixed Execute(ctx) entry keeps dispatch an
// interface call - reflection runs once at registration, never per invocation.
public enum CommandPermission { Universal = 0, Admin = 1, Owner = 2 }

[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandAttribute : Attribute
{
    public string FullName;                  // "<modId>:<name>", e.g. "minecraft:give"
    public CommandPermission Permission = CommandPermission.Universal;
    public bool RawArgs;                     // raw mode: skip the parse plan, Execute reads ctx.Args itself

    public CommandAttribute(string fullName) { FullName = fullName; }
}

[AttributeUsage(AttributeTargets.Field)]
public sealed class ParamAttribute : Attribute
{
    public readonly int Index;               // -1 = unassigned: fill the remaining slots in declaration order
    public ParamAttribute(int index = -1) { Index = index; }
}

public interface ICommand { void Execute(CommandContext ctx); }

public abstract class CommandBase : ResourceType, ICommand
{
    // Auto-generated at registration from the parse plan ("/give <item> [amount]").
    public string Usage { get; internal set; }
    // Minimum level the dispatcher gate checks before Execute.
    public CommandPermission Permission { get; internal set; } = CommandPermission.Universal;
    // Parse plan built at registration; null = raw mode (Execute parses ctx.Args itself).
    internal CommandPlan Plan;
    public abstract void Execute(CommandContext ctx);
}

// Logic-side settlement surface (design §5.1). Reply/Error are only valid
// during the current Execute call (synchronous echo, D2).
public class CommandContext
{
    public string[] Args;         // tokenized; Args[0] = command name as typed
    public int SourceEntityId;    // DTO-friendly (future process boundary)
    public int PermissionLevel;   // caller's level, resolved by the dispatcher's permission table
    public Action<string> Reply;  // success/plain feedback line
    public Action<string> Error;  // error feedback line
}
