using UnityEngine;

// Named source of truth for the numbers every panel used to re-hardcode
// (P0 of Docs/UI布局系统-原版机制与Forge生态调研及优化方案.md). All values
// equal the original literals, so the migration is visually zero-change.
public static class UIStyle
{
    public const float PanelWidth = 920f;
    public const float PanelHeight = 420f;
    public const float PanelScale = 0.8f;        // authored 1:1, drawn at 0.8
    public const float SlotSize = 100f;          // slot rect (was the RectTransform default)
    public const float SlotPitch = 100f;         // center-to-center, backpack / hotbar
    public const float SlotPitchCompact = 110f;  // workbench / 2x2 grid pitch (kept distinct)
    public const float CountFontSize = 20f;      // stack-count label
    public static readonly Vector3 PanelOffset = new(0f, 160f, 0f);
    public static readonly Vector3 PlayerInvOffset = new(0f, 50f, 0f);
}
