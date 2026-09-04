using TMPro;
using UnityEngine;

// Text label widget in the same shape as UIIconWidget: self-built component +
// static factory, anchored to the parent's center, size decided by the caller.
// raycastTarget off: purely presentational, never swallows pointer hits.
public class UITextWidget : MonoBehaviour
{
    private TextMeshProUGUI text = null;
    private void Awake()
    {
        text = gameObject.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        // GO comes with a RectTransform from the factory (see UIIconWidget
        // comment: Awake-time transform replacement is deferred).
        RectTransform rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 20;
        text.color = Color.black;
    }

    public void SetText(string value)
    {
        if (value == null) return;
        text.text = value;
    }

    public void SetFontSize(float size) => text.fontSize = size;

    public void SetColor(Color color) => text.color = color;

    public void SetAlignment(TextAlignmentOptions alignment) => text.alignment = alignment;

    public static GameObject CreateNewText(string text = "", GameObject parent = null)
    {
        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.AddComponent<UITextWidget>().SetText(text);
        if(parent != null)textGo.transform.SetParent(parent.transform, false);
        return textGo;
    }
}
