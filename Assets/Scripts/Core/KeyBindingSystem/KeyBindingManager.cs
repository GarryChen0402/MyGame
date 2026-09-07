using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Runtime service over the frozen ResourceSystem.KeyBindings table: keeps the
// per-action user overrides (defaults live in the registry) plus per-frame
// state, and answers the two semantic queries game code polls instead of
// reading physical keys:
//   - IsDown      : held (movement, continuous digging)
//   - WasPressed  : buffered edge, one consumption per press (open/close UI...)
// Edge presses are routed by context: a press only feeds an action whose
// AllowedInputHandlers whitelist admits the current top-of-stack input
// handler, so the same physical key may mean different actions in different
// contexts (E: open_inventory in game, close_ui in a panel) without either
// double-firing. Push/Pop clears the buffer (see InputHandlerManager).
public class KeyBindingManager
{
    private static KeyBindingManager instance = new();
    public static KeyBindingManager Instance => instance;

    private KeyBindingManager()
    {
        // Registries are complete only after the bootstrap freezes them, so
        // validation and the override load hook onto BootstrapCompleted.
        EventBus.Instance.Subscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
    }

    // ---- per-action user overrides (absent = registry default) + frame state ----
    private readonly Dictionary<string, (KeyCode key, KeyModifier modifier)> overrides = new();
    private readonly Dictionary<string, bool> down = new();
    private readonly Dictionary<string, int> pendingClicks = new();

    // Main key -> actions bound to it (defaults + overrides). Rebuilt only when
    // bindings or overrides change, so per-frame refresh scans pressed keys.
    private Dictionary<KeyCode, List<string>> actionsByKey = new();
    private bool keyIndexDirty = true;

    private bool initialized;
    private readonly HashSet<string> warnedUnknown = new();

    // ---- per-frame state refresh (called by InputHandlerManager.Update) ----

    public void RefreshInput()
    {
        if(keyIndexDirty)RebuildKeyIndex();

        bool ctrl = AnyModifierDown(KeyModifier.Ctrl);
        bool shift = AnyModifierDown(KeyModifier.Shift);
        bool alt = AnyModifierDown(KeyModifier.Alt);
        string topHandler = InputHandlerManager.Instance != null
            ? InputHandlerManager.Instance.CurrentInputHandler?.FullName : null;

        // Held state: physical full sweep, independent of context (context is
        // enforced on the query side via IsAllowed).
        foreach(var binding in ResourceSystem.Instance.KeyBindings.Values)
        {
            GetCurrentBinding(binding, out var key, out var modifier);
            down[binding.FullName] = Input.GetKey(key) && ModifierHeld(modifier, ctrl, shift, alt);
        }

        // Edges: each physical press is routed to at most one action - the one
        // bound to that key whose whitelist admits the current context. A held
        // modifier chord (Ctrl+B) wins over the bare press (B) on the same key.
        foreach(var pair in actionsByKey)
        {
            if(!Input.GetKeyDown(pair.Key))continue;
            string chosen = RoutePress(pair.Value, topHandler, ctrl, shift, alt);
            if(chosen == null)continue;
            pendingClicks[chosen] = pendingClicks.TryGetValue(chosen, out int count) ? count + 1 : 1;
        }
    }

    private string RoutePress(List<string> candidates, string topHandler, bool ctrl, bool shift, bool alt)
    {
        string bare = null;
        foreach(string fullName in candidates)
        {
            if(!ResourceSystem.Instance.KeyBindings.TryGetResourceWithFullName(fullName, out var binding))continue;
            if(!ContextAllows(binding, topHandler))continue;
            GetCurrentBinding(binding, out _, out var modifier);
            if(modifier == KeyModifier.None)
            {
                if(bare == null)bare = fullName;
                continue;
            }
            if(ModifierHeld(modifier, ctrl, shift, alt))return fullName;   // chord beats bare press
        }
        return bare;
    }

    // ---- semantic queries (game logic polls these, never physical keys) ----

    // Whether the current top-of-stack handler may trigger this action
    // (null/empty whitelist = unrestricted); defensive check for non-handler code.
    public bool IsAllowed(string bindingFullName)
    {
        if(string.IsNullOrEmpty(bindingFullName))return false;   // no action bound to the session yet
        if(!ResourceSystem.Instance.KeyBindings.TryGetResourceWithFullName(bindingFullName, out var binding))
        {
            WarnUnknown(bindingFullName);
            return false;
        }
        return ContextAllows(binding, InputHandlerManager.Instance != null
            ? InputHandlerManager.Instance.CurrentInputHandler?.FullName : null);
    }

    public bool IsDown(string bindingFullName)
        => IsAllowed(bindingFullName)
           && down.TryGetValue(bindingFullName, out bool isDown) && isDown;

    // Consumes one buffered press (pendingClicks > 0) and returns true; the
    // buffer is routing-fed and cleared on handler Push/Pop, so presses are
    // never lost to late consumers nor leaked across context switches.
    public bool WasPressed(string bindingFullName)
    {
        if(!IsAllowed(bindingFullName))return false;
        if(pendingClicks.TryGetValue(bindingFullName, out int count) && count > 0)
        {
            pendingClicks[bindingFullName] = count - 1;
            return true;
        }
        return false;
    }

    public void ClearPendingClicks() => pendingClicks.Clear();

    // ---- rebinding ----

    public void SetBinding(string bindingFullName, KeyCode key,
        KeyModifier modifier = KeyModifier.None, bool persistent = true)
    {
        if(!ResourceSystem.Instance.KeyBindings.TryGetResourceWithFullName(bindingFullName, out var binding))
        {
            WarnUnknown(bindingFullName);
            return;
        }
        if(!binding.AllowRebind)
        {
            Debug.LogWarning($"[KeyBinding] {bindingFullName} is marked non-rebindable, change rejected");
            return;
        }
        if(!IsBindableKey(key))
        {
            Debug.LogWarning($"[KeyBinding] key {key} is reserved (Esc / None) and cannot be bound to {bindingFullName}");
            return;
        }
        GetCurrentBinding(binding, out var oldKey, out var oldModifier);
        overrides[bindingFullName] = (key, modifier);
        keyIndexDirty = true;
        if(oldKey != key || oldModifier != modifier)
            Debug.Log($"[KeyBinding] {bindingFullName} rebound: {Format(oldKey, oldModifier)} -> {Format(key, modifier)}");
        if(persistent)SaveOverrides();
    }

    public void ResetBinding(string bindingFullName, bool persistent = true)
    {
        if(!ResourceSystem.Instance.KeyBindings.TryGetResourceWithFullName(bindingFullName, out var binding))
        {
            WarnUnknown(bindingFullName);
            return;
        }
        if(!overrides.Remove(bindingFullName))return;
        keyIndexDirty = true;
        GetCurrentBinding(binding, out var key, out var modifier);
        Debug.Log($"[KeyBinding] {bindingFullName} reset to default {Format(key, modifier)}");
        if(persistent)SaveOverrides();
    }

    // ---- bootstrap hooks ----

    private void OnBootstrapCompleted(BootstrapCompletedEvent evt)
    {
        if(initialized || !evt.Success)return;
        initialized = true;
        LoadOverrides();
        ValidateAll();
    }

    // Post-freeze sanity sweep: whitelist handler ids must exist; same
    // (key, modifier) on whitelist-overlapping actions is a real conflict
    // (late registration wins, only warned), on mutually exclusive whitelists
    // it is legitimate context reuse (E: open/close). Warnings only, never fatal.
    private void ValidateAll()
    {
        foreach(var binding in ResourceSystem.Instance.KeyBindings.Values)
        {
            if(binding.AllowedInputHandlers == null)continue;
            foreach(string handlerId in binding.AllowedInputHandlers)
            {
                if(!ResourceSystem.Instance.InputHandlers.TryGetResourceWithFullName(handlerId, out _))
                    Debug.LogWarning($"[KeyBinding] {binding.FullName} whitelists unknown input handler '{handlerId}'");
            }
        }

        var list = new List<KeyBinding>(ResourceSystem.Instance.KeyBindings.Values);
        for(int i = 0; i < list.Count; i++)
        {
            GetCurrentBinding(list[i], out var keyI, out var modI);
            if(!IsBindableKey(keyI))
                Debug.LogWarning($"[KeyBinding] {list[i].FullName} has unbindable default key {keyI}");
            for(int j = i + 1; j < list.Count; j++)
            {
                GetCurrentBinding(list[j], out var keyJ, out var modJ);
                if(keyI != keyJ || modI != modJ)continue;
                if(WhitelistsOverlap(list[i].AllowedInputHandlers, list[j].AllowedInputHandlers))
                    Debug.LogWarning($"[KeyBinding] conflict: {list[i].FullName} and {list[j].FullName} both bound to {Format(keyI, modI)} with overlapping contexts (later registration wins)");
                else
                    Debug.Log($"[KeyBinding] {list[i].FullName} and {list[j].FullName} share {Format(keyI, modI)} in disjoint contexts (context reuse)");
            }
        }
    }

    // ---- overrides: defaults (registry) layered under user overrides (file) ----

    private static string OverrideFilePath()
        => Path.Combine(Application.persistentDataPath, "settings", "keybindings.json");

    public void LoadOverrides()
    {
        string path = OverrideFilePath();
        if(!File.Exists(path))return;
        try
        {
            foreach(var pair in ParseOverrideFile(File.ReadAllText(path)))
            {
                if(!ResourceSystem.Instance.KeyBindings.TryGetResourceWithFullName(pair.Key, out var binding))
                {
                    Debug.LogWarning($"[KeyBinding] save file holds unknown action '{pair.Key}', skipped");
                    continue;
                }
                if(!binding.AllowRebind)
                {
                    Debug.LogWarning($"[KeyBinding] save file rebinds non-rebindable '{pair.Key}', skipped");
                    continue;
                }
                if(!IsBindableKey(pair.Value.key))
                {
                    Debug.LogWarning($"[KeyBinding] save file binds {pair.Key} to reserved key {pair.Value.key}, skipped");
                    continue;
                }
                overrides[pair.Key] = (pair.Value.key, pair.Value.modifier);
                keyIndexDirty = true;
            }
        }
        catch(Exception e)
        {
            Debug.LogWarning($"[KeyBinding] failed to load {path}: {e.Message}; defaults stay in effect");
        }
    }

    public void SaveOverrides()
    {
        string path = OverrideFilePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sb = new StringBuilder("{\"bindings\":{");
            bool first = true;
            foreach(var pair in overrides)
            {
                if(!first)sb.Append(',');
                first = false;
                sb.Append('"').Append(pair.Key).Append("\":");
                sb.Append(FormatBindingValue(pair.Value.key, pair.Value.modifier));
            }
            sb.Append("}}");
            File.WriteAllText(path, sb.ToString());
        }
        catch(Exception e)
        {
            Debug.LogWarning($"[KeyBinding] failed to save {path}: {e.Message}");
        }
    }

    private static string FormatBindingValue(KeyCode key, KeyModifier modifier)
    {
        if(modifier == KeyModifier.None)
            return $"\"{key}\"";   // bare string = no modifier (legacy-friendly form)
        return $"{{\"key\":\"{key}\",\"modifier\":\"{modifier.ToString().ToLower()}\"}}";
    }

    // ---- helpers ----

    private void RebuildKeyIndex()
    {
        actionsByKey.Clear();
        foreach(var binding in ResourceSystem.Instance.KeyBindings.Values)
        {
            GetCurrentBinding(binding, out var key, out _);
            if(!IsBindableKey(key))continue;
            if(!actionsByKey.TryGetValue(key, out var list))actionsByKey[key] = list = new List<string>();
            list.Add(binding.FullName);
        }
        keyIndexDirty = false;
    }

    private void GetCurrentBinding(KeyBinding binding, out KeyCode key, out KeyModifier modifier)
    {
        key = binding.DefaultKey;
        modifier = binding.DefaultModifier;
        if(overrides.TryGetValue(binding.FullName, out var o))
        {
            key = o.key;
            modifier = o.modifier;
        }
    }

    private static bool ContextAllows(KeyBinding binding, string topHandler)
    {
        var whitelist = binding.AllowedInputHandlers;
        if(whitelist == null || whitelist.Count == 0)return true;
        if(topHandler == null)return false;
        return whitelist.Contains(topHandler);
    }

    private static bool WhitelistsOverlap(HashSet<string> a, HashSet<string> b)
    {
        if(a == null || a.Count == 0 || b == null || b.Count == 0)return true;
        foreach(string id in a)if(b.Contains(id))return true;
        return false;
    }

    private static bool AnyModifierDown(KeyModifier modifier) => modifier switch
    {
        KeyModifier.Ctrl  => Input.GetKey(KeyCode.LeftControl)  || Input.GetKey(KeyCode.RightControl),
        KeyModifier.Shift => Input.GetKey(KeyCode.LeftShift)    || Input.GetKey(KeyCode.RightShift),
        KeyModifier.Alt   => Input.GetKey(KeyCode.LeftAlt)      || Input.GetKey(KeyCode.RightAlt),
        _ => false
    };

    private static bool ModifierHeld(KeyModifier modifier, bool ctrl, bool shift, bool alt) => modifier switch
    {
        KeyModifier.None  => true,
        KeyModifier.Ctrl  => ctrl,
        KeyModifier.Shift => shift,
        KeyModifier.Alt   => alt,
        _ => false
    };

    // Esc is a system key (close top panel / menus) and never enters the
    // binding table; KeyCode.None means "unbound".
    private static bool IsBindableKey(KeyCode key)
        => key != KeyCode.None && key != KeyCode.Escape;

    private static string Format(KeyCode key, KeyModifier modifier)
        => modifier == KeyModifier.None ? key.ToString() : $"{modifier.ToString().ToLower()}+{key}";

    private void WarnUnknown(string bindingFullName)
    {
        if(warnedUnknown.Add(bindingFullName))
            Debug.LogWarning($"[KeyBinding] unknown action '{bindingFullName}' queried (mod unloaded?)");
    }

    // ---- keybindings.json reader ----
    // Grammar (design doc §7, option A):
    //   { "bindings": { "action:full.name": "KEY" | { "key": "KEY", "modifier": "ctrl" } } }
    // A bare string is a rebind without modifier. JsonUtility cannot handle
    // this shape (keyed object with mixed value types), so a strict scanner
    // parses it; malformed input fails the whole file with a warning and the
    // registry defaults stay in effect.

    private static Dictionary<string, (KeyCode key, KeyModifier modifier)> ParseOverrideFile(string text)
    {
        var result = new Dictionary<string, (KeyCode, KeyModifier)>();
        int i = 0;
        Expect(text, ref i, '{');
        bool rootDone = false;
        while(!rootDone)
        {
            string member = ParseString(text, ref i);
            Expect(text, ref i, ':');
            if(member == "bindings")ParseBindingsObject(text, ref i, result);
            else SkipValue(text, ref i);
            SkipWhitespace(text, ref i);
            if(Peek(text, i) == ','){ i++; continue; }
            Expect(text, ref i, '}');
            rootDone = true;
        }
        SkipWhitespace(text, ref i);
        if(i != text.Length)throw new FormatException("trailing content");
        return result;
    }

    private static void ParseBindingsObject(string text, ref int i,
        Dictionary<string, (KeyCode key, KeyModifier modifier)> result)
    {
        Expect(text, ref i, '{');
        SkipWhitespace(text, ref i);
        if(Peek(text, i) == '}'){ i++; return; }
        while(true)
        {
            SkipWhitespace(text, ref i);
            string action = ParseString(text, ref i);
            Expect(text, ref i, ':');
            SkipWhitespace(text, ref i);
            KeyModifier modifier = KeyModifier.None;
            string keyName;
            if(Peek(text, i) == '"')keyName = ParseString(text, ref i);
            else
            {
                // Object form: { "key": ..., "modifier": ... }.
                Expect(text, ref i, '{');
                keyName = null;
                while(true)
                {
                    SkipWhitespace(text, ref i);
                    string field = ParseString(text, ref i);
                    Expect(text, ref i, ':');
                    SkipWhitespace(text, ref i);
                    string value = ParseString(text, ref i);
                    if(field == "key")keyName = value;
                    else if(field == "modifier")
                    {
                        if(!TryParseModifier(value, out modifier))
                            throw new FormatException($"unknown modifier '{value}' for '{action}'");
                    }
                    SkipWhitespace(text, ref i);
                    if(Peek(text, i) == ','){ i++; continue; }
                    Expect(text, ref i, '}');
                    break;
                }
                if(keyName == null)throw new FormatException($"'{action}' object entry has no \"key\"");
            }
            if(!Enum.TryParse(keyName, true, out KeyCode key))
                throw new FormatException($"unknown key '{keyName}' for '{action}'");
            result[action] = (key, modifier);
            SkipWhitespace(text, ref i);
            if(Peek(text, i) == ','){ i++; continue; }
            Expect(text, ref i, '}');
            return;
        }
    }

    private static bool TryParseModifier(string value, out KeyModifier modifier)
    {
        switch(value.ToLowerInvariant())
        {
            case "none":  modifier = KeyModifier.None;  return true;
            case "ctrl":  modifier = KeyModifier.Ctrl;  return true;
            case "shift": modifier = KeyModifier.Shift; return true;
            case "alt":   modifier = KeyModifier.Alt;   return true;
            default:      modifier = KeyModifier.None;  return false;
        }
    }

    private static void SkipValue(string text, ref int i)
    {
        SkipWhitespace(text, ref i);
        char c = Peek(text, i);
        if(c == '"'){ ParseString(text, ref i); return; }
        if(c == '{' || c == '[')
        {
            char close = c == '{' ? '}' : ']';
            i++;
            int depth = 1;
            while(depth > 0)
            {
                char d = Peek(text, i);
                if(d == '"'){ ParseString(text, ref i); continue; }
                i++;
                if(d == c)depth++;
                else if(d == close)depth--;
            }
            return;
        }
        // number/bool/null token, ends at the enclosing ',' or '}'
        while(i < text.Length)
        {
            char d = Peek(text, i);
            if(d == ',' || d == '}')break;
            i++;
        }
    }

    private static void Expect(string text, ref int i, char expected)
    {
        SkipWhitespace(text, ref i);
        if(i >= text.Length || text[i] != expected)
            throw new FormatException($"expected '{expected}' at position {i}");
        i++;
    }

    private static char Peek(string text, int i) => i < text.Length ? text[i] : '\0';

    private static void SkipWhitespace(string text, ref int i)
    {
        while(i < text.Length && char.IsWhiteSpace(text[i]))i++;
    }

    private static string ParseString(string text, ref int i)
    {
        SkipWhitespace(text, ref i);
        if(i >= text.Length || text[i] != '"')throw new FormatException($"expected string at position {i}");
        i++;
        var sb = new StringBuilder();
        while(true)
        {
            if(i >= text.Length)throw new FormatException("unterminated string");
            char c = text[i++];
            if(c == '"')return sb.ToString();
            if(c != '\\'){ sb.Append(c); continue; }
            if(i >= text.Length)throw new FormatException("unterminated escape");
            char e = text[i++];
            switch(e)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'u':
                    if(i + 4 > text.Length)throw new FormatException("bad unicode escape");
                    sb.Append((char)Convert.ToInt32(text.Substring(i, 4), 16));
                    i += 4;
                    break;
                default: throw new FormatException($"unknown escape '\\{e}'");
            }
        }
    }
}
