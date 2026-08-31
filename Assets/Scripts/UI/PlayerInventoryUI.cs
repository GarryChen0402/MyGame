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


        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        var image = bgGo.AddComponent<Image>();
        rt = bgGo.GetOrAddComponent<RectTransform>();
        image.type = Image.Type.Sliced;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        image.sprite = Resources.Load<Sprite>("Textures/UI/universal_bg");

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
}