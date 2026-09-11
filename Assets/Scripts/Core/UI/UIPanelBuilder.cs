using UnityEngine;

// Panel frame / canvas root scaffolding shared by the UI layer (P0 of
// Docs/UI布局系统-原版机制与Forge生态调研及优化方案.md): the repeated frame
// and stretch-root boilerplate collapses here, with the numbers named in
// UIStyle instead of re-hardcoded per panel.
public static class UIPanelBuilder
{
    // Panel frame from the layout data (anchor/scale/offset), plus its
    // background unless the layout declares none (null = no background).
    // Element positions are relative to this rect; pivot stays (0.5,0.5) so
    // the anchor point is the panel center (design §4.1 定位语义).
    public static RectTransform BuildFrame(UIBehavior host, PanelLayout layout)
    {
        var rt = host.gameObject.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = layout.Anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(layout.Width, layout.Height);
        rt.localScale = new Vector3(layout.Scale, layout.Scale, 1f);
        rt.anchoredPosition = layout.Offset;

        if(layout.Background != null)
        {
            var bg = UIWidgetBackground.CreateNewBackground(
                UISprites.Resolve(layout.Background.Sprite, "minecraft:universal_bg"));
            bg.transform.SetParent(host.transform, false);
            bg.GetComponent<UIWidgetBackground>().SetColor(layout.Background.Tint);
        }
        return rt;
    }

    // Full-stretch child of a canvas root (anchors cover the parent, no local
    // offset): every UIManager root and the loading overlay use it.
    public static RectTransform BuildStretchRoot(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }
}
