using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Integer-only text input widget (TMP_InputField wrapper), same shape as
// UIIconWidget/UITextWidget: self-built component + static factory, anchored
// to the parent's center, size decided by the caller. Keystrokes are gated by
// CharacterValidation.Integer (digits, leading '-' only); a sanitize pass
// strips anything else so pasted text also stays a valid integer. Hosts drive
// the red-frame "invalid value" look through SetInvalid and commit handling
// mirrors UITextInputWidget: OnValueChanged fires per keystroke (live overlay
// previews), OnSubmit on Enter / focus loss (apply).
public class UIIntegerInputWidget : MonoBehaviour
{
    private TMP_InputField inputField;
    private Image bg;
    private static readonly Color NormalColor = new(1f, 1f, 1f, 0.6f);
    private static readonly Color InvalidColor = new(1f, 0.5f, 0.5f, 0.75f);

    private void Awake()
    {
        inputField = gameObject.AddComponent<TMP_InputField>();
        inputField.characterValidation = TMP_InputField.CharacterValidation.Integer;

        // The input's text area: TMP_InputField requires a child TMP text as
        // its textComponent; stretch it over the field's rect with a small
        // padding so the caret does not clip at the edges.
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

        inputField.onValueChanged.AddListener(_ => Sanitize());
        SetValue(0);
    }

    // Fires with the raw field text on Enter or focus loss. Hosts parse back
    // through GetValue (empty / minus-only fields parse as 0).
    public TMP_InputField.SubmitEvent OnSubmit => inputField.onEndEdit;

    // Fires per keystroke - hosts use it to live-update dependent UI without
    // waiting for a commit.
    public TMP_InputField.OnChangeEvent OnValueChanged => inputField.onValueChanged;

    // Pasted text bypasses keystroke validation, so every change is re-checked
    // and repaired (SetTextWithoutNotify keeps this from re-triggering the
    // listener).
    private void Sanitize()
    {
        string cleaned = Clean(inputField.text);
        if(cleaned != inputField.text) inputField.SetTextWithoutNotify(cleaned);
    }

    private static string Clean(string value)
    {
        var sb = new StringBuilder(value.Length);
        for(int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if(char.IsDigit(c)) sb.Append(c);
            else if(c == '-' && i == 0 && sb.Length == 0) sb.Append(c);   // leading minus only
        }
        return sb.ToString();
    }

    // Host-driven validation look: red frame while invalid (the host decides
    // validity; the widget only paints the state).
    public void SetInvalid(bool invalid) => bg.color = invalid ? InvalidColor : NormalColor;

    // Empty / minus-only fields parse as 0.
    public int GetValue() => int.TryParse(inputField.text, out int v) ? v : 0;

    public void SetValue(int value) => inputField.SetTextWithoutNotify(value.ToString());

    public static GameObject CreateNewIntegerInput(int initialValue = 0, GameObject parent = null)
    {
        GameObject inputGo = new GameObject("Integer Input", typeof(RectTransform));
        var widget = inputGo.AddComponent<UIIntegerInputWidget>();
        if(initialValue != 0)widget.SetValue(initialValue);
        if(parent != null)inputGo.transform.SetParent(parent.transform, false);
        return inputGo;
    }
}
