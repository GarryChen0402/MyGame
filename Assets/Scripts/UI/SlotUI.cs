using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotUI : MonoBehaviour
{
    private ItemIconRenderer Icon;
    private TextMeshProUGUI Text;
    private ItemStack itemStack;

    private void Awake()
    {   
        var bg_image = gameObject.AddComponent<Image>();
        bg_image.sprite = Resources.Load<Sprite>("Textures/UI/slot");
        // var edgeGo = new GameObject("edge");
        // edgeGo.transform.SetParent(gameObject.transform);
        // edgeGo.AddComponent<Image>().sprite = Resources.Load<Sprite>("Textures/UI/slot_ui_edge");

        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(gameObject.transform, false);
        iconGo.AddComponent<RectTransform>();
        iconGo.AddComponent<RawImage>();
        Icon = iconGo.AddComponent<ItemIconRenderer>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(gameObject.transform, false);
        textGo.transform.localPosition = new Vector3(-2, -25, 0);
        textGo.AddComponent<RectTransform>();

        Text = textGo.AddComponent<TextMeshProUGUI>();
        Text.alignment = TextAlignmentOptions.BottomRight;
        Text.fontSize = 20;
        Text.color = Color.black;
        Text.fontStyle = FontStyles.Bold;

        // TMP's OnEnable overwrites sizeDelta, so set it after AddComponent.
        var textRect = (RectTransform)Text.transform;
        textRect.sizeDelta = new Vector2(100, textRect.sizeDelta.y);   // width = 100

    }

    // Bind the slot to an item stack and refresh the icon and count label.
    public void SetItemStack(ItemStack stack)
    {
        itemStack = stack;
        if(stack == null || stack.IsEmpty())
        {
            Icon.gameObject.SetActive(false);
            Text.text = "";
            return;
        }

        Icon.gameObject.SetActive(true);
        Icon.SetItem(stack.itemId);
        Text.text = stack.amount > 1 ? stack.amount.ToString() : "";
    }
}
