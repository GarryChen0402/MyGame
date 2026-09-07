using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Single-line free-text input widget in the same shape as UIIntegerInputWidget:
// self-built component + static factory, anchored to the parent's center, size
// decided by the caller. Accepts any text (no validation); the submit event
// fires on Enter or focus loss. Hosts drive the red-frame "invalid value"
// look through SetInvalid.
public class UITextInputWidget : MonoBehaviour
{
    private TMP_InputField inputField;
    private Image bg;
    private static readonly Color NormalColor = new(1f, 1f, 1f, 0.6f);
    private static readonly Color InvalidColor = new(1f, 0.5f, 0.5f, 0.75f);

    private void Awake()
    {
        inputField = gameObject.AddComponent<TMP_InputField>();

        // The input's text area (same layout as UIIntegerInputWidget): stretch
        // over the field's rect with a small padding so the caret never clips.
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(6, 2);
        textRt.offsetMax = new Vector2(-6, -2);
        var tmpText = textGo.AddComponent<TextMeshProUGUI>();
        tmpText.raycastTarget = false;
        tmpText.alignment = TextAlignmentOptions.Left;
        tmpText.fontSize = 20;
        tmpText.color = Color.black;
        inputField.textComponent = tmpText;

        // Subtle backdrop marks the input area; without a graphic on this GO
        // EventSystem could never hit the field to focus it, and the same
        // image doubles as the transition / invalid-state graphic.
        bg = gameObject.AddComponent<Image>();
        bg.color = NormalColor;
        inputField.targetGraphic = bg;

        // GO comes with a RectTransform from the factory (Awake-time transform
        // replacement is deferred by Unity, so never AddComponent one here).
        RectTransform rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
    }

    // Fires with the field's text when the edit commits (Enter or focus loss).
    public TMP_InputField.SubmitEvent OnSubmit => inputField.onEndEdit;

    public string GetText() => inputField.text;

    public void SetText(string value) => inputField.SetTextWithoutNotify(value ?? "");

    // Host-driven validation look: red frame while invalid (the host decides
    // validity; the widget only paints the state).
    public void SetInvalid(bool invalid) => bg.color = invalid ? InvalidColor : NormalColor;

    public static GameObject CreateNewTextInput(string text = "", string placeholder = null,
                                                GameObject parent = null)
    {
        GameObject inputGo = new GameObject("Text Input", typeof(RectTransform));
        var widget = inputGo.AddComponent<UITextInputWidget>();

        // Placeholder is appended after Awake built the field: the grey hint
        // child must be the field's sibling rendered behind the text, exactly
        // like uGUI's standard placeholder wiring.
        if(!string.IsNullOrEmpty(placeholder))
        {
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(inputGo.transform, false);
            var phRt = (RectTransform)phGo.transform;
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(6, 2);
            phRt.offsetMax = new Vector2(-6, -2);
            var phText = phGo.AddComponent<TextMeshProUGUI>();
            phText.raycastTarget = false;
            phText.alignment = TextAlignmentOptions.Left;
            phText.fontSize = 20;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.45f, 0.45f, 0.45f, 0.9f);
            phText.text = placeholder;
            var field = inputGo.GetComponent<TMP_InputField>();
            if(field != null)field.placeholder = phText;
        }

        widget.SetText(text);
        if(parent != null)inputGo.transform.SetParent(parent.transform, false);
        return inputGo;
    }
}
