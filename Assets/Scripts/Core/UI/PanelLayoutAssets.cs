using System;
using System.Collections.Generic;
using UnityEngine;

// JSON asset form of PanelLayout (L1 of Docs/可视化UI布局编辑器-实施文档.md,
// design §4.2): JsonUtility cannot serialize the polymorphic element list, so
// the on-disk shape is a flat tagged union - kind plus the full field set -
// with ToData/ToLayout as the only converters (shared by the runtime loader
// and the editor export command). ToData writes every field explicitly so
// assets are self-describing; ToLayout tolerates omissions in hand-written
// files and substitutes defaults there, never relying on DTO initializers.

[Serializable]
public class SpriteRefData
{
    public string sprite;
    public Color tint = Color.white;
}

[Serializable]
public class PanelLayoutData
{
    public int formatVersion = PanelLayoutSerializer.FormatVersion;
    public string panel;
    public float width = UIStyle.PanelWidth;
    public float height = UIStyle.PanelHeight;
    public float scale = UIStyle.PanelScale;
    public Vector2 offset = new(UIStyle.PanelOffset.x, UIStyle.PanelOffset.y);
    public Vector2 anchor = new(0.5f, 0.5f);
    // Null = no background; the exporter writes the default explicitly.
    public SpriteRefData background;
    public List<PanelElementData> elements = new();
}

// Tagged union: "kind" is the single dispatch key ("slot" | "grid" | "bar",
// design §4.2 元素工厂=kind 分派); the unused fields stay at their defaults.
[Serializable]
public class PanelElementData
{
    public string kind;
    public string id;
    public string idPrefix;
    public Vector2 pos;
    public Vector2 origin;
    public int rows, cols;
    public float pitch;
    public int dir;                      // ProgressBarUI.Direction enum ordinal
    public SpriteRefData frame;
    public SpriteRefData front, back;
}

public static class PanelLayoutSerializer
{
    public const int FormatVersion = 1;

    public static PanelLayoutData ToData(PanelLayout layout, string panelName)
    {
        var data = new PanelLayoutData
        {
            formatVersion = FormatVersion,
            panel = panelName,
            width = layout.Width,
            height = layout.Height,
            scale = layout.Scale,
            offset = new Vector2(layout.Offset.x, layout.Offset.y),
            anchor = layout.Anchor,
            background = ToData(layout.Background)
        };
        foreach(var element in layout.Elements)
        {
            switch(element)
            {
                case SlotElement slot:
                    data.elements.Add(new PanelElementData
                    {
                        kind = "slot", id = slot.Id, pos = slot.Pos, frame = ToData(slot.Frame)
                    });
                    break;
                case GridSlotElement grid:
                    data.elements.Add(new PanelElementData
                    {
                        kind = "grid", idPrefix = grid.IdPrefix,
                        rows = grid.Rows, cols = grid.Cols, pitch = grid.Pitch, origin = grid.Origin
                    });
                    break;
                case BarElement bar:
                    data.elements.Add(new PanelElementData
                    {
                        kind = "bar", id = bar.Id, pos = bar.Pos, dir = (int)bar.Dir,
                        front = ToData(bar.Front), back = ToData(bar.Back)
                    });
                    break;
            }
        }
        return data;
    }

    // Returns null on an unrecognized version - the caller falls back to the
    // C# static layout (missing/corrupt assets must never throw).
    public static PanelLayout ToLayout(PanelLayoutData data)
    {
        if(data == null)return null;
        if(data.formatVersion != FormatVersion)
        {
            Debug.LogWarning($"[PanelLayoutAssets] unsupported formatVersion {data.formatVersion} (expected {FormatVersion})");
            return null;
        }
        var layout = new PanelLayout
        {
            Width = data.width > 0f ? data.width : UIStyle.PanelWidth,
            Height = data.height > 0f ? data.height : UIStyle.PanelHeight,
            Scale = data.scale > 0f ? data.scale : UIStyle.PanelScale,
            Anchor = data.anchor,
            Offset = new Vector3(data.offset.x, data.offset.y, 0f),
            Background = ToRef(data.background)
        };
        if(data.elements == null)return layout;
        foreach(var element in data.elements)
        {
            switch(element.kind)
            {
                case "slot":
                    if(string.IsNullOrEmpty(element.id)){ WarnSkip("slot without id"); continue; }
                    layout.Elements.Add(new SlotElement
                    {
                        Id = element.id, Pos = element.pos, Frame = ToRef(element.frame)
                    });
                    break;
                case "grid":
                    if(string.IsNullOrEmpty(element.idPrefix)){ WarnSkip("grid without idPrefix"); continue; }
                    layout.Elements.Add(new GridSlotElement
                    {
                        IdPrefix = element.idPrefix, Rows = element.rows, Cols = element.cols,
                        Pitch = element.pitch, Origin = element.origin
                    });
                    break;
                case "bar":
                    if(string.IsNullOrEmpty(element.id)){ WarnSkip("bar without id"); continue; }
                    layout.Elements.Add(new BarElement
                    {
                        Id = element.id, Pos = element.pos, Dir = (ProgressBarUI.Direction)element.dir,
                        Front = ToRef(element.front), Back = ToRef(element.back)
                    });
                    break;
                default:
                    WarnSkip($"unknown element kind '{element.kind}'");
                    break;
            }
        }
        return layout;
    }

    private static SpriteRef ToRef(SpriteRefData data)
        => data == null ? null : new SpriteRef(data.sprite, data.tint == default ? Color.white : data.tint);

    private static SpriteRefData ToData(SpriteRef reference)
        => reference == null ? null : new SpriteRefData { sprite = reference.Sprite, tint = reference.Tint };

    private static void WarnSkip(string reason)
        => Debug.LogWarning($"[PanelLayoutAssets] element skipped: {reason}");
}

// Registration-time layout resolution (E2/D3): load the JSON asset, convert,
// and fall back to the C# static layout on any miss with a single warning.
// Called once per definition by UIDefs.SinglePanel - no open-time IO.
public static class PanelLayoutAssets
{
    public static PanelLayout Load(string resourcePath, PanelLayout fallback)
    {
        var asset = Resources.Load<TextAsset>(resourcePath);
        if(asset == null)
        {
            Debug.LogWarning($"[PanelLayoutAssets] '{resourcePath}' not found; using the C# static layout");
            return fallback;
        }
        try
        {
            var layout = PanelLayoutSerializer.ToLayout(JsonUtility.FromJson<PanelLayoutData>(asset.text));
            if(layout != null)return layout;
        }
        catch(Exception e)
        {
            Debug.LogWarning($"[PanelLayoutAssets] failed to parse '{resourcePath}': {e.Message}");
        }
        Debug.LogWarning($"[PanelLayoutAssets] '{resourcePath}' rejected; using the C# static layout");
        return fallback;
    }
}
