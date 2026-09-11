using System.Collections.Generic;
using UnityEngine;

public class HotBarUI : UIBehavior
{
    private static HotBarUI instance = null;
    public static HotBarUI Instance => instance;
    [SerializeField]
    private List<SlotUI> itemIcons = new();

    private bool bound;
    private void Awake()
    {
        if(instance == null)instance = this;
        else Destroy(gameObject);
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0);   // screen center
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, 50);

        for(int i = 0; i < 9; i++)
        {
            var slotGo = new GameObject($"hotbar_slot_{i}");
            slotGo.transform.SetParent(gameObject.transform, false);
            var slotUI = slotGo.AddComponent<SlotUI>();
            slotGo.transform.localPosition = new Vector3(-400 + i * 100, 0, 0);
            itemIcons.Add(slotUI);
        }

        // rt.sizeDelta = new Vector2(30, 30);   // 15px texture doubled for visibility

    }

    // Phase C: reads the resident backpack mirror instead of the live
    // inventory. SlotUI.Refresh diffs internally, so a per-frame sweep only
    // re-renders changed slots. Hotbar slots never bind interactive - they
    // stay unclickable, exactly like before.
    private void Update()
    {
        var mirror = MirrorSync.Instance?.PlayerInventoryMirror;
        if(mirror == null)return;   // resident sources register when the player is created
        if(!bound)
        {
            for(int i = 0; i < itemIcons.Count; i++)itemIcons[i].BindDisplay(mirror, i);
            bound = true;
            return;
        }
        for(int i = 0; i < itemIcons.Count; i++)itemIcons[i].Refresh();
    }

    public static UIDefinition hotbarDefinition = new()
    {
        modId = "minecraft",
        name = "hotbar",
        OpenWithPlayerInventory = false,
        Panel = BuildPanel(),
        Factory = () =>
        {
            var hotBarGo = new GameObject("HotBar");
            hotBarGo.AddComponent<HotBarUI>();
            return hotBarGo;
        }
    };

    // Resident binding carrier (P0, A9): 9 hand-written slots, codes written
    // in code for now (moved to layout JSON at L4). The HUD row and the
    // backpack's bottom row map to the same container cells; actionIds stay
    // empty - the HUD is display-only today and R/U arrive via JEI's
    // PreFreeze injection (P7).
    private static PanelDescriptor BuildPanel()
    {
        var panel = new PanelDescriptor();
        for(int i = 0; i < 9; i++)
            panel.Bindings[$"hotbar_{i}"] = new SlotBindingEntry($"slot_{i}", new ItemDataParser());
        return panel;
    }
}