using System.Collections.Generic;
using UnityEngine;

// Declarative panel geometry (S3 of Docs/改造提案-UI数据流拆分方案.md, merged
// with P1 of Docs/UI布局系统-原版机制与Forge生态调研及优化方案.md): one pure
// data description per panel, interpreted by PanelLayoutRunner. Slot ids are
// the alignment keys shared with the container-reported data names
// (PanelData.SlotIndex) and with the descriptor's open-time validation.
// L1 of Docs/可视化UI布局编辑器-实施文档.md added the frame/scaling/background
// fields and SpriteRef; every default equals the former hardcoded value, so
// existing static layouts keep their exact visuals.

// Minimal appearance reference (design §4.7): a logical sprite id plus a tint.
// Vanilla ids may drop the "minecraft:" prefix; resolution goes through
// UISprites.Resolve. Null in a slot/bar field means "no sprite"/"default".
public class SpriteRef
{
    public string Sprite;
    public Color Tint = Color.white;

    public SpriteRef() { }
    public SpriteRef(string sprite) { Sprite = sprite; }
    public SpriteRef(string sprite, Color tint) { Sprite = sprite; Tint = tint; }
}

public class PanelLayout
{
    public float Width = UIStyle.PanelWidth;
    public float Height = UIStyle.PanelHeight;
    public Vector3 Offset = UIStyle.PanelOffset;
    // Normalized anchor (anchorMin == anchorMax), panel pivot stays (0.5,0.5)
    // and Offset lands in anchoredPosition (design §4.1 定位语义).
    public Vector2 Anchor = new(0.5f, 0.5f);
    public float Scale = UIStyle.PanelScale;
    // Null = no background (hotbar); the C# default stays the current backdrop
    // so panels that never touch the field keep it. In JSON assets a missing
    // background also means "none" - the exporter always writes it explicitly.
    public SpriteRef Background = new("minecraft:universal_bg");
    public readonly List<PanelElement> Elements = new();
}

public abstract class PanelElement
{
    // Data slot names this element binds, in element order. Bars contribute
    // none: their data rides the descriptor's channel names instead.
    public abstract void CollectSlotNames(List<string> names);
}

// Single slot at Pos, panel-local.
public class SlotElement : PanelElement
{
    public string Id;
    public Vector2 Pos;
    // Null = the shared default slot frame (minecraft:slot).
    public SpriteRef Frame;

    public override void CollectSlotNames(List<string> names) => names.Add(Id);
}

// Slot grid expanded to "IdPrefix0".."IdPrefixN-1", row-major with rows
// top-to-bottom; Origin is the center of the top-left cell.
public class GridSlotElement : PanelElement
{
    public string IdPrefix;
    public int Rows = 1, Cols = 1;
    public float Pitch;
    public Vector2 Origin;

    public override void CollectSlotNames(List<string> names)
    {
        for(int i = 0; i < Rows * Cols; i++)names.Add(IdPrefix + i);
    }
}

// Progress gauge; Front/Back carry logical sprite ids (a null Back keeps the
// no-backdrop semantics of the former string field).
public class BarElement : PanelElement
{
    public string Id;
    public Vector2 Pos;
    public ProgressBarUI.Direction Dir;
    public SpriteRef Front;
    public SpriteRef Back = null;

    public override void CollectSlotNames(List<string> names) { }
}
