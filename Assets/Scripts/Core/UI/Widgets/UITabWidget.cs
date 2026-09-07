using System;
using System.Collections.Generic;
using UnityEngine;

// Tab bar control (design doc UI组件化重构设计方案 §3.4 #5): a row of
// equal-width tab buttons across the top edge and a content area below
// (vanilla creative-inventory style category switching). Pages are built on
// demand: AddTab registers (id, label, builder); switching destroys the
// current page and re-invokes the builder, so pages always carry fresh data.
//
// Usage: set the root's size first, then AddTab repeatedly; the first tab is
// selected automatically. Call Reflow() if the root size changes at runtime.
public class UITabWidget : MonoBehaviour
{
    public class TabEntry
    {
        public string Id;
        public string Label;
        public Func<GameObject> BuildContent;   // returns an unparented GO; the widget parents it into the content area
    }

    private static readonly Color IdleColor = new(1f, 1f, 1f, 0.4f);
    private static readonly Color SelectedColor = new(1f, 1f, 1f, 1f);

    private readonly List<TabEntry> entries = new();
    private readonly List<UIButtonWidget> tabButtons = new();
    private RectTransform contentRt;
    private GameObject contentGo;
    private float tabHeight = 28;
    private float tabGap = 0;
    private int currentIndex = -1;

    private void Awake()
    {
        // The root GO comes with a RectTransform from the factory (Awake-time
        // transform replacement is deferred by Unity, so never AddComponent one
        // here); center anchoring matches the sibling widgets.
        RectTransform rootRt = (RectTransform)transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);

        var contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(transform, false);
        contentRt = (RectTransform)contentObj.transform;
    }

    public string CurrentTabId => currentIndex >= 0 ? entries[currentIndex].Id : null;

    public void AddTab(string id, string label, Func<GameObject> buildContent)
    {
        int index = entries.Count;
        entries.Add(new TabEntry { Id = id, Label = label, BuildContent = buildContent });

        var buttonGo = UIButtonWidget.CreateNewButton(label, null, gameObject);
        var button = buttonGo.GetComponent<UIButtonWidget>();
        int captured = index;
        button.OnClick.AddListener(() => SelectTabAt(captured));
        tabButtons.Add(button);

        if(currentIndex < 0)SelectTabAt(index);
        Reflow();
    }

    public void SelectTab(string id)
    {
        for(int i = 0; i < entries.Count; i++)
            if(entries[i].Id == id){ SelectTabAt(i); return; }
    }

    public void SelectTabAt(int index)
    {
        if(index < 0 || index >= entries.Count)return;
        if(index == currentIndex)return;   // re-selecting the live tab does not rebuild
        currentIndex = index;

        if(contentGo != null){ Destroy(contentGo); contentGo = null; }
        var entry = entries[index];
        if(entry.BuildContent != null)
        {
            contentGo = entry.BuildContent();
            if(contentGo != null)contentGo.transform.SetParent(contentRt, false);
        }
        UpdateHighlights();
    }

    private void UpdateHighlights()
    {
        for(int i = 0; i < tabButtons.Count; i++)
            tabButtons[i].SetColor(i == currentIndex ? SelectedColor : IdleColor);
    }

    // Equal-width buttons across the top, content area below them. Recompute
    // after AddTab and whenever the root's size changes at runtime.
    public void Reflow()
    {
        var rootRt = (RectTransform)transform;
        float rootW = rootRt.rect.width;
        float rootH = rootRt.rect.height;
        int n = tabButtons.Count;
        if(n == 0 || rootW <= 0)return;

        float w = (rootW - tabGap * (n - 1)) / n;
        for(int i = 0; i < n; i++)
        {
            var buttonRt = (RectTransform)tabButtons[i].transform;
            buttonRt.sizeDelta = new Vector2(w, tabHeight);
            buttonRt.localPosition = new Vector3(-rootW / 2f + w * (i + 0.5f) + tabGap * i, rootH / 2f - tabHeight / 2f, 0);
        }

        // Content fills the root below the tab strip.
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = new Vector2(0, -tabHeight);
    }

    public void SetTabHeight(float height)
    {
        tabHeight = height;
        Reflow();
    }

    public void SetTabGap(float gap)
    {
        tabGap = gap;
        Reflow();
    }

    public static GameObject CreateNewTab(GameObject parent = null)
    {
        GameObject tabGo = new GameObject("Tab UI", typeof(RectTransform));
        tabGo.AddComponent<UITabWidget>();
        if(parent != null)tabGo.transform.SetParent(parent.transform, false);
        return tabGo;
    }
}
