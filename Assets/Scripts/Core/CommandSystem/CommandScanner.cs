using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Registration-time scan (design §5.1): every [Command]-marked CommandBase in
// the assembly becomes a Commands-table entry - instance created once, full
// name split into modId/name, parse plan built, registered sorted by full
// name (stable /help output). Same-family move as BootstrapperDriver's IMod
// discovery (GetExecutingAssembly + interface filter + Activator), same
// "explicit ordering" discipline; no reflection runs per invocation.
public static class CommandScanner
{
    public static void ScanAndRegister(Assembly assembly)
    {
        var found = new List<CommandBase>();
        var seen = new HashSet<string>();
        foreach(var type in assembly.GetTypes())
        {
            if(!typeof(CommandBase).IsAssignableFrom(type) || type.IsAbstract)continue;
            var attr = type.GetCustomAttribute<CommandAttribute>();
            if(attr == null)
            {
                Debug.LogWarning($"[CommandScanner] {type.Name} extends CommandBase but lacks [Command]; skipped");
                continue;
            }
            string fullName = attr.FullName;
            int separator = fullName?.IndexOf(':') ?? -1;
            if(separator <= 0 || separator == fullName.Length - 1)
            {
                Debug.LogError($"[CommandScanner] {type.Name}: invalid command full name '{fullName}' (expected \"<modId>:<name>\")");
                continue;
            }
            if(!seen.Add(fullName))
            {
                Debug.LogError($"[CommandScanner] duplicate command full name '{fullName}' ({type.Name}); skipped");
                continue;
            }
            var command = (CommandBase)Activator.CreateInstance(type);
            command.modId = fullName.Substring(0, separator);
            command.name = fullName.Substring(separator + 1);
            command.Permission = attr.Permission;
            if(attr.RawArgs)
            {
                // Raw mode: no parse plan; Execute reads ctx.Args itself (escape hatch).
            }
            else
            {
                if(!CommandPlan.TryBuild(type, command, out var plan))
                {
                    Debug.LogError($"[CommandScanner] {type.Name}: parse plan build failed; skipped");
                    continue;
                }
                command.Plan = plan;
            }
            found.Add(command);
        }
        found.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));
        foreach(var command in found)
        {
            if(!ResourceSystem.Instance.Commands.Register(command))
                Debug.LogError($"[CommandScanner] register rejected '{command.FullName}' (duplicate or table frozen)");
        }
    }
}
