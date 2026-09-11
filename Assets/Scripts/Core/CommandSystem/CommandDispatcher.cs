using System;
using System.Collections.Generic;
using UnityEngine;

// Inbound DTO (design §5.8): the UI side only ever enqueues this - parsing and
// settlement happen inside the 20Hz tick (D2). A future process split promotes
// it to the inbound message unchanged.
public class CommandRequest
{
    public string Raw;
    public int SourceEntityId;
}

// Logic-side dispatcher (D2): consumes the queue once per tick (hooked into
// WorldManager.Tick), tokenizes, resolves through ResourceSystem.Commands,
// gates on permission, fills parameters via the command's parse plan, then
// runs Execute. No UI dependency - echo goes through the logic-side ChatLog.
public class CommandDispatcher
{
    public static CommandDispatcher Instance { get; } = new();

    private static readonly char[] Whitespace = { ' ', '\t' };

    private readonly Queue<CommandRequest> queue = new();

    // Main thread on both ends (input frame enqueues, tick drains) - no locking.
    public void Enqueue(CommandRequest request)
    {
        if(request != null)queue.Enqueue(request);
    }

    public void Tick()
    {
        while(queue.Count > 0)Settle(queue.Dequeue());
    }

    private static void Settle(CommandRequest request)
    {
        // The submit line itself enters history first (vanilla behavior); it
        // also makes parse failures visible in the panel.
        ChatLog.Instance.Add(request.Raw ?? string.Empty, false);
        var ctx = new CommandContext
        {
            SourceEntityId = request.SourceEntityId,
            Reply = line => ChatLog.Instance.Add(line, false),
            Error = line => ChatLog.Instance.Add(line, true)
        };

        string raw = (request.Raw ?? string.Empty).Trim();
        if(!raw.StartsWith("/"))
        {
            ctx.Error("Input must start with / (e.g. /help)");
            return;
        }
        string[] tokens = raw.Substring(1).Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
        if(tokens.Length == 0)
        {
            ctx.Error("Missing command name (see /help)");
            return;
        }
        ctx.Args = tokens;

        // Short names resolve in the minecraft: namespace; mod commands need
        // their full name (design §5.2).
        string name = tokens[0];
        string fullName = name.IndexOf(':') >= 0 ? name : "minecraft:" + name;
        if(!ResourceSystem.Instance.Commands.TryGetResourceWithFullName(fullName, out var command))
        {
            ctx.Error($"Unknown command: {name} (see /help)");
            return;
        }

        ctx.PermissionLevel = ResolvePermission(request);
        if(ctx.PermissionLevel < (int)command.Permission)
        {
            ctx.Error("Permission denied");
            return;
        }

        if(command.Plan != null && !command.Plan.TryFill(command, tokens, out string error))
        {
            ctx.Error(error);
            return;
        }

        try
        {
            command.Execute(ctx);
        }
        catch(Exception e)
        {
            // Mod code must never break the tick chain.
            Debug.LogError($"[CommandDispatcher] '{command.FullName}' threw: {e}");
            ctx.Error("Command execution failed");
        }
    }

    // Permission lookup (design §5.8): single-player has exactly one source -
    // the local player, always Owner. The real table lands in the server
    // process when the server splits out; only this lookup changes then.
    private static int ResolvePermission(CommandRequest request) => (int)CommandPermission.Owner;
}
