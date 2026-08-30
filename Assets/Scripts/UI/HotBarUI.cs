using System.Collections.Generic;
using UnityEngine;

public class HotBarUI : MonoBehaviour
{
    private static HotBarUI instance = null;
    public static HotBarUI Instance => instance;
    [SerializeField]
    private List<SlotUI> itemIcons = new();

    // Cached slot contents for dirty checking; UI is refreshed only on change.
    private readonly ushort[] cachedItemIds = new ushort[9];
    private readonly int[] cachedAmounts = new int[9];

    private void Awake()
    {
        if(instance == null)instance = this;
        else Destroy(gameObject);

        for(int i = 0; i < 9; i++)
        {
            var slotGo = new GameObject($"hotbar_slot_{i}");
            slotGo.transform.SetParent(gameObject.transform);
            var slotUI = slotGo.AddComponent<SlotUI>();
            slotGo.transform.localPosition = new Vector3(-400 + i * 100, 0, 0);
            itemIcons.Add(slotUI);
        }
    }

    private void Update()
    {
        if(Player.Instance == null)return;
        var inventory = Player.Instance.inventory;
        for(int i = 0; i < 9; i++)
        {
            var stack = inventory.GetItemStackAt(i);
            ushort id = stack?.itemId ?? 0;
            int amount = stack?.amount ?? 0;
            if(cachedItemIds[i] == id && cachedAmounts[i] == amount)continue;
            cachedItemIds[i] = id;
            cachedAmounts[i] = amount;
            itemIcons[i].SetItemStack(stack);
        }
    }
}