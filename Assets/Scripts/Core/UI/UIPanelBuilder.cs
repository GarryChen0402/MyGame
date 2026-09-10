using UnityEngine;

// Panel frame / canvas root scaffolding shared by the UI layer (P0 of
// Docs/UI布局系统-原版机制与Forge生态调研及优化方案.md): the repeated frame
// and stretch-root boilerplate collapses here, with the numbers named in
// UIStyle instead of re-hardcoded per panel.
public static class UIPanelBuilder
{
    // Center-anchored panel frame sized from the layout data, plus its
    // background. Element positions are relative to this rect.
    public static RectTransform BuildFrame(UIBehavior host, float width, float height, Vector3 offset)
    {
        var rt = host.gameObject.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.localScale = new Vector3(UIStyle.PanelScale, UIStyle.PanelScale, 1f);
        rt.localPosition += offset;

        var bg = UIWidgetBackground.CreateNewBackground();
        bg.transform.SetParent(host.transform, false);
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
