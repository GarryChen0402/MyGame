using System;
using UnityEngine;

public class UIDefinition : ResourceType
{
    public UIKind Kind;
    public string InputHandlerId = null;
    public bool OpenWithPlayerInventory = false;
    // Container panels only: how to parse and validate the PanelData packet
    // (S3 of Docs/改造提案-UI数据流拆分方案.md); null for plain UIs.
    public PanelDescriptor Panel = null;
    public Func<GameObject> Factory = null;
}