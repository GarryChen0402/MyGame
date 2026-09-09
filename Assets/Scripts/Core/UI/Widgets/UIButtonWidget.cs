using UnityEngine;
using UnityEngine.UI;

// Button widget in the same shape as UIIconWidget/UITextWidget: self-built
// component + static factory, anchored to the parent's center, size decided
// by the caller. Packs the uGUI Button (background Image doubles as the
// transition graphic) with a child UITextWidget label; click wiring happens
// on the exposed OnClick event.
public class UIButtonWidget : MonoBehaviour
{
    private Image bg;
    private Button button;
    private UITextWidget label;
    private void Awake()
    {
        bg = gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.8f);

        button = gameObject.AddComponent<Button>();
        button.targetGraphic = bg;

        var labelGo = UITextWidget.CreateNewText("", gameObject);
        var labelRt = (RectTransform)labelGo.transform;
        labelRt.anchorMin = Vector2.zero;   // stretch over the button face
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        label = labelGo.GetComponent<UITextWidget>();

        // GO comes with a RectTransform from the factory (Awake-time transform
        // replacement is deferred by Unity, so never AddComponent one here).
        RectTransform rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    public Button.ButtonClickedEvent OnClick => button.onClick;

    public void SetText(string text) => label.SetText(text);

    // Background tint: lets hosts draw selection state (tab highlight etc.).
    public void SetColor(Color color) => bg.color = color;

    public void SetFontSize(float size) => label.SetFontSize(size);

    public void SetSprite(Sprite sprite)
    {
        if(sprite == null)return;
        bg.sprite = sprite;
    }

    // Interactable passthrough: grey-out placeholder buttons (e.g. the main
    // menu's disabled entries) ride on uGUI's ColorTint disabled state.
    public void SetInteractable(bool value) => button.interactable = value;

    public static GameObject CreateNewButton(string text = "", Sprite sprite = null, GameObject parent = null)
    {
        GameObject buttonGo = new GameObject("Button", typeof(RectTransform));
        var widget = buttonGo.AddComponent<UIButtonWidget>();
        widget.SetSprite(sprite);
        widget.SetText(text);
        if(parent != null)buttonGo.transform.SetParent(parent.transform, false);
        return buttonGo;
    }
}
