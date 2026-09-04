using System.Collections.Generic;
using UnityEngine;

public enum KeyModifier
{ 
    None,
    Ctrl,
    Alt,
    Shift
}

public class KeyBinding : ResourceType
{
    public KeyCode DefaultKey;
    public KeyModifier DefaultModifier = KeyModifier.None;
    public string Category;
    public bool AllowRebind = true;
    public HashSet<string> AllowedInputHandlers = null;
}