using System;
using UnityEngine;

public class UIDefinition : ResourceType
{
    public UIKind Kind;
    public string InputHandlerId = null;
    public bool OpenWithPlayerInventory = false;
    public Func<GameObject> Factory = null;
}