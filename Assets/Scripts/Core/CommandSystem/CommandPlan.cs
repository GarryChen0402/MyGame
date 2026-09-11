using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;

// One positional parameter slot of a command: the backing field, whether it
// may be omitted, the default value captured from a fresh instance (reset
// before every fill - no residue between calls, D10), and the type tag used
// by conversion and error text.
public class ParamSlot
{
    public FieldInfo Field;
    public bool Optional;
    public object DefaultValue;
    public string Label;      // field name, as shown in the usage line
    public string TypeName;   // int / float / bool / string
}

// Parse plan (design §5.1/§5.2): built once at registration from the
// [Param]-marked public fields, consumed per invocation by TryFill. Slot
// assignment follows D9 - [Param(n)] pins a slot, unmarked-index fields fill
// the remaining slots in declaration order. Duplicate / out-of-range slots,
// required-after-optional order, and field types without a converter are all
// registration-time errors (fail loudly).
public class CommandPlan
{
    public readonly List<ParamSlot> Slots = new();
    public string Usage;      // "/give <item> [amount]"

    public static bool TryBuild(Type commandType, CommandBase defaultInstance, out CommandPlan plan)
    {
        plan = null;
        var fields = commandType.GetFields(BindingFlags.Instance | BindingFlags.Public);
        var marked = new List<(FieldInfo Field, int Index)>();
        foreach(var field in fields)
        {
            var attr = field.GetCustomAttribute<ParamAttribute>();
            if(attr != null) marked.Add((field, attr.Index));
        }

        int slotCount = marked.Count;
        var slots = new ParamSlot[slotCount];
        // Explicit [Param(n)] indexes first: duplicates / out-of-range fail here.
        foreach(var (field, index) in marked)
        {
            if(index < 0) continue;
            if(index >= slotCount)
            {
                Debug.LogError($"[CommandPlan] {commandType.Name}: [Param({index})] on '{field.Name}' out of range ({slotCount} slots)");
                return false;
            }
            if(slots[index] != null)
            {
                Debug.LogError($"[CommandPlan] {commandType.Name}: duplicate slot {index} ('{field.Name}')");
                return false;
            }
            slots[index] = BuildSlot(field, defaultInstance, commandType);
            if(slots[index] == null)return false;
        }
        // Unmarked-index fields fill the remaining slots in declaration order.
        int next = 0;
        foreach(var (field, index) in marked)
        {
            if(index >= 0) continue;
            while(next < slotCount && slots[next] != null) next++;
            if(next >= slotCount)
            {
                Debug.LogError($"[CommandPlan] {commandType.Name}: no free slot for '{field.Name}'");
                return false;
            }
            slots[next] = BuildSlot(field, defaultInstance, commandType);
            if(slots[next] == null)return false;
        }
        bool seenOptional = false;
        for(int i = 0; i < slotCount; i++)
        {
            if(slots[i] == null)
            {
                Debug.LogError($"[CommandPlan] {commandType.Name}: slot {i} left empty");
                return false;
            }
            if(slots[i].Optional)seenOptional = true;
            else if(seenOptional)
            {
                Debug.LogError($"[CommandPlan] {commandType.Name}: required param '{slots[i].Label}' after an optional one");
                return false;
            }
        }

        var usage = new StringBuilder("/").Append(defaultInstance.name);
        foreach(var slot in slots)usage.Append(slot.Optional ? $" [{slot.Label}]" : $" <{slot.Label}>");
        plan = new CommandPlan { Usage = usage.ToString() };
        plan.Slots.AddRange(slots);
        defaultInstance.Usage = plan.Usage;
        return true;
    }

    private static ParamSlot BuildSlot(FieldInfo field, object defaultInstance, Type commandType)
    {
        if(!TryGetKind(field.FieldType, out string kind, out bool nullable))
        {
            Debug.LogError($"[CommandPlan] {commandType.Name}: '{field.Name}' type {field.FieldType.Name} has no converter (int/float/bool/string)");
            return null;
        }
        object defaultValue = field.GetValue(defaultInstance);
        return new ParamSlot
        {
            Field = field,
            Optional = nullable || HasInitializer(field.FieldType, defaultValue),
            DefaultValue = defaultValue,
            Label = field.Name,
            TypeName = kind
        };
    }

    // Nullable<T> is always optional (absent = null); for the rest, a fresh
    // instance carrying a non-default value means the declarer wrote an
    // initializer, which doubles as the omitted-value default.
    private static bool HasInitializer(Type fieldType, object value)
    {
        if(value == null)return false;
        if(fieldType == typeof(string))return true;
        return !value.Equals(Activator.CreateInstance(fieldType));
    }

    private static bool TryGetKind(Type fieldType, out string kind, out bool nullable)
    {
        nullable = false;
        Type type = fieldType;
        Type underlying = Nullable.GetUnderlyingType(fieldType);
        if(underlying != null)
        {
            nullable = true;
            type = underlying;
        }
        if(type == typeof(int))kind = "int";
        else if(type == typeof(float))kind = "float";
        else if(type == typeof(bool))kind = "bool";
        else if(type == typeof(string))kind = "string";
        else { kind = null; return false; }
        return true;
    }

    // tokens[0] = the command name; positional args start at tokens[1].
    public bool TryFill(CommandBase instance, string[] tokens, out string error)
    {
        int argCount = tokens.Length - 1;
        foreach(var slot in Slots)slot.Field.SetValue(instance, slot.DefaultValue);   // D10: reset before fill
        for(int i = 0; i < argCount; i++)
        {
            if(i >= Slots.Count)
            {
                error = $"Too many arguments; usage: {Usage}";
                return false;
            }
            var slot = Slots[i];
            if(!TryConvert(tokens[i + 1], slot, out object value))
            {
                error = $"Invalid argument {slot.Label}: \"{tokens[i + 1]}\" (expected {slot.TypeName}); usage: {Usage}";
                return false;
            }
            slot.Field.SetValue(instance, value);
        }
        if(argCount < Slots.Count && !Slots[argCount].Optional)
        {
            error = $"Not enough arguments; usage: {Usage}";
            return false;
        }
        error = null;
        return true;
    }

    private static bool TryConvert(string token, ParamSlot slot, out object value)
    {
        switch(slot.TypeName)
        {
            case "string":
                value = token;
                return true;
            case "int":
                if(int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                {
                    value = i;
                    return true;
                }
                break;
            case "float":
                if(float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                {
                    value = f;
                    return true;
                }
                break;
            case "bool":
                if(bool.TryParse(token, out bool b))
                {
                    value = b;
                    return true;
                }
                break;
        }
        value = null;
        return false;
    }
}
