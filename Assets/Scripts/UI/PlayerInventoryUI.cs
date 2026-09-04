using System.Collections.Generic;
using System.IO.Compression;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInventoryUI : UIBehavior
{
    private List<SlotUI> slots = new();
    private void Awake()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.25f);
        rt.anchorMax = new Vector2(0.5f, 0.25f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(920, 420);
        rt.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        rt.localPosition += new Vector3(0, 50, 0);

        var bgGo = UIWidgetBackground.CreateNewBackground();
        bgGo.transform.SetParent(transform, false);

        // x [-400, 400]
        int[] xPos = {-400, -300, -200, -100, 0, 100, 200, 300, 400};
        // y [-160, 160, 60, 40]
        int[] yPos = {-160, 160, 60, -40};
        for(int y = 0; y < 4; y++)
        {
            for(int x = 0; x < 9; x++)
            {
                var go = new GameObject($"{y*9+x}");
                var slotUI = go.AddComponent<SlotUI>();
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(xPos[x], yPos[y], 0);
                slotUI.SetItemStack(Player.Instance.inventory.GetItemStackAt(y * 9 + x));
                slots.Add(slotUI);
            }
        }
    }

    public static UIDefinition playerInvUIDefinition = new()
    {
        modId = "minecraft",
        name = "player_inventory",
        Kind = UIKind.PlayerInventory,
        OpenWithPlayerInventory = false,
        Factory = () =>
        {
            var go = new GameObject("Player Inventory");
            go.AddComponent<PlayerInventoryUI>();
            return go;
        }
    };

    private void OnEnable()
    {
        // Rebind click access (player slots have no container policy) and the
        // displayed stack; both point at the same Inventory slot objects.
        for(int i = 0; i < 36; i++)
        {
            slots[i].Bind(new PlayerSlotAccess(Player.Instance.inventory, i));
            slots[i].SetItemStack(Player.Instance.inventory.GetItemStackAt(i));
        }
    }

    // Click resolution mutates the bound stacks in place; Refresh sweeps every
    // slot to reflect merges / swaps / quick-moves.
    public override void Refresh()
    {
        for(int i = 0; i < slots.Count; i++)slots[i].Refresh();
    }

    // Live sync while the panel is open: SlotUI.Refresh is nearly free when
    // nothing changed (it compares its last-rendered state against the bound
    // stack and only re-renders the 3D icon on a difference), so a per-frame
    // sweep also picks up direct inventory writes - give commands, pickups,
    // crafting - that never pass through the click path. Unity skips Update
    // on inactive GameObjects, so this runs only while the panel is visible.
    private void Update()
    {
        for(int i = 0; i < slots.Count; i++)slots[i].Refresh();
    }
}