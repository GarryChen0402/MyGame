using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Chat panel (P1 of Docs/指令系统-实施文档.md): vanilla-style shell - no solid
// panel box; history lines carry their own translucent black bars and stack
// bottom-up above a single-line input row (the 2026-09-11 rework against the
// vanilla reference screenshot). Enter submits (enqueue DTO; all parsing and
// settlement happens in the logic tick, D2) and closes the panel; Esc closes
// without submitting - the input widget's blur-submit is deliberately left
// unwired for that reason. Opening prefills "/" (D7): the mandatory prefix
// becomes the default experience and the open-frame key race (the "/" that
// opened the panel never reaches the field) is absorbed. Code-built shell like
// WidgetTestUI - the layout schema has no text/input elements.
public class ChatPanelUI : UIBehavior
{
    private const int MaxLines = 20;           // newest N entries; scrolling arrives in P2
    private const float WrapWidth = 900f;      // history bar / input row width cap
    private const float LeftMargin = 4f;
    private const float InputBottomY = 130f;   // clears the hotbar row
    private const float InputHeight = 30f;
    private const float LineMinHeight = 24f;
    private const float LinePadX = 6f;         // matches the input widget's text inset
    private static readonly Color BarColor = new(0f, 0f, 0f, 0.35f);
    private static readonly Color PlainColor = Color.white;
    private static readonly Color ErrorColor = new(1f, 1f / 3f, 1f / 3f);   // vanilla error red

    private UITextInputWidget input;
    private TMP_InputField inputField;
    private readonly List<Line> lines = new();
    private Material shadowMaterial;
    private int shownVersion = -1;
    private bool focusPending;

    // Read by ChatHudUI: the transient HUD yields (freezes hidden) while the
    // full chat panel is on screen, vanilla-style.
    public static bool IsOpen { get; private set; }

    private class Line
    {
        public RectTransform root;
        public TextMeshProUGUI text;
    }

    private void Awake()
    {
        // Full-screen transparent root: children anchor to the screen's bottom
        // left, so the history column can grow upward without a box.
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var inputGo = UITextInputWidget.CreateNewTextInput("", null, gameObject);
        input = inputGo.GetComponent<UITextInputWidget>();
        inputField = inputGo.GetComponent<TMP_InputField>();
        var inputRt = (RectTransform)inputGo.transform;
        inputRt.anchorMin = inputRt.anchorMax = new Vector2(0f, 0f);
        inputRt.pivot = new Vector2(0f, 0f);
        inputRt.sizeDelta = new Vector2(WrapWidth, InputHeight);
        // anchoredPosition, not localPosition: the parent stretches the full
        // screen with its pivot at the centre, so a raw localPosition would
        // land at screen-centre + offset instead of hugging the corner.
        inputRt.anchoredPosition = new Vector2(LeftMargin, InputBottomY);
        // Vanilla input: white text over a subtle dark strip, not the widget's
        // default light box (the image stays as the raycast/transition target).
        inputGo.GetComponent<Image>().color = BarColor;
        inputField.textComponent.color = Color.white;
        inputField.caretColor = Color.white;
        inputField.selectionColor = new Color(1f, 1f, 1f, 0.35f);
        ApplyShadow(inputField.textComponent);
    }

    private void OnEnable()
    {
        IsOpen = true;
        if(input == null)return;
        input.SetText("/");   // D7: every fresh open starts from the prefilled prefix
        focusPending = true;
        FocusInput();
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if(focusPending)
        {
            // TMP can miss ActivateInputField in the very frame the panel
            // activates; retry once on the next Update.
            focusPending = false;
            if(inputField != null && !inputField.isFocused)FocusInput();
        }
        HandleSubmit();
        RefreshHistory();
    }

    private void FocusInput()
    {
        if(inputField == null)return;
        if(EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        inputField.ActivateInputField();
        inputField.caretPosition = inputField.text.Length;
    }

    private void HandleSubmit()
    {
        if(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Submit();
    }

    // The raw text is read before closing; an emptied line still reaches the
    // dispatcher and fails prefix validation with an echo (never silently
    // dropped) - closing is UIManager's regular close path (pops the input
    // handler, re-locks the cursor).
    private void Submit()
    {
        string raw = input != null ? input.GetText() : null;
        var player = Player.Instance;
        CommandDispatcher.Instance.Enqueue(new CommandRequest
        {
            Raw = raw,
            SourceEntityId = player != null ? player.EntityId : 0
        });
        UIManager.Instance?.CloseUI();
    }

    // Version-gated redraw: the mirror snapshots on the render frame, so a
    // rebuild only happens when a command actually wrote to the log. Newest
    // entry sits lowest (vanilla); bars hug the text width.
    private void RefreshHistory()
    {
        var mirror = MirrorSync.Instance.ChatLogMirror;
        if(mirror == null || mirror.Version == shownVersion)return;
        shownVersion = mirror.Version;
        int count = mirror.Lines?.Length ?? 0;
        int shown = Mathf.Min(count, MaxLines);
        int start = count - shown;
        float y = InputBottomY + InputHeight + 8f;
        for(int k = shown - 1; k >= 0; k--)
        {
            int idx = start + k;
            Line line = EnsureLine(k);
            line.root.gameObject.SetActive(true);
            line.text.text = mirror.Lines[idx] ?? "";
            line.text.color = mirror.Errors[idx] ? ErrorColor : PlainColor;
            // Measure against the full wrap width first, then shrink the bar
            // to the line's own width.
            line.root.sizeDelta = new Vector2(WrapWidth, LineMinHeight);
            line.text.ForceMeshUpdate();
            float width = Mathf.Clamp(line.text.preferredWidth + LinePadX * 2f, 24f, WrapWidth);
            float height = Mathf.Max(LineMinHeight, line.text.preferredHeight + 2f);
            line.root.sizeDelta = new Vector2(width, height);
            line.root.anchoredPosition = new Vector2(LeftMargin, y);
            y += height;
        }
        for(int k = shown; k < lines.Count; k++)lines[k].root.gameObject.SetActive(false);
    }

    private Line EnsureLine(int index)
    {
        while(lines.Count <= index)lines.Add(CreateLine(lines.Count));
        return lines[index];
    }

    private Line CreateLine(int index)
    {
        var go = new GameObject($"Line {index}", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(WrapWidth, LineMinHeight);
        var bar = go.AddComponent<Image>();
        bar.color = BarColor;
        bar.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(LinePadX, 0f);
        textRt.offsetMax = new Vector2(-LinePadX, 0f);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        // Rich text off: user input must never reach a rich-text parser
        // (per-line coloring goes through text.color; escaping stays P2 work).
        text.richText = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.fontSize = 20;
        text.overflowMode = TextOverflowModes.Overflow;
        ApplyShadow(text);
        return new Line { root = rt, text = text };
    }

    // Shared underlay material: the vanilla hard drop shadow (an offset dark
    // copy of the glyphs). One instance for the whole panel - a per-text
    // instance would leak one material per line.
    private void ApplyShadow(TMP_Text text)
    {
        if(shadowMaterial == null)
        {
            shadowMaterial = new Material(text.fontSharedMaterial);
            shadowMaterial.EnableKeyword("UNDERLAY_ON");
            shadowMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.55f));
            shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 2f);
            shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -2f);
            shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0f);
            shadowMaterial.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
        }
        text.fontMaterial = shadowMaterial;
    }

    public static UIDefinition chatPanelUIDefinition = new()
    {
        modId = "minecraft",
        name = "chat_panel",
        Kind = UIKind.SinglePanel,
        // Dedicated chat context, never the shared ui_input_handler: JEI's
        // world-panel follow keys off that name and must not fire while
        // chatting (see ChatInputHandler).
        InputHandlerId = "minecraft:chat_input_handler",
        OpenWithPlayerInventory = false,   // vanilla: no backpack strip while chatting
        Factory = () =>
        {
            var go = new GameObject("Chat Panel", typeof(ChatPanelUI));
            return go;
        }
    };
}
