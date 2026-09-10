using System.Collections.Generic;
using UnityEngine;

// Declarative panel geometry (S3 of Docs/改造提案-UI数据流拆分方案.md, merged
// with P1 of Docs/UI布局系统-原版机制与Forge生态调研及优化方案.md): one pure
// data description per panel, interpreted by PanelLayoutRunner. Slot ids are
// the alignment keys shared with the container-reported data names
// (PanelData.SlotIndex) and with the descriptor's open-time validation.
public class PanelLayout
{
    public float Width = UIStyle.PanelWidth;
    public float Height = UIStyle.PanelHeight;
    public Vector3 Offset = UIStyle.PanelOffset;
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

// Progress gauge; Front/Back are sprite names under Resources/Textures/UI.
public class BarElement : PanelElement
{
    public string Id;
    public Vector2 Pos;
    public ProgressBarUI.Direction Dir;
    public string Front;
    public string Back = null;

    public override void CollectSlotNames(List<string> names) { }
}
