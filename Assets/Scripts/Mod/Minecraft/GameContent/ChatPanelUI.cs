using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Chat panel (P1 of Docs/指令系统-实施文档.md): vanilla-style shell - no solid
// panel box; history lines carry their own translucent black bars and stack
// bottom-up above a single-line input row (the 2026-09-11 rework against the
// vanilla reference screenshot); P2: the wheel browses the log - the window
// is height-bounded, opens at the bottom and slides into history (vanilla).
// Enter submits (enqueue DTO; all parsing and
// settlement happens in the logic tick, D2) and closes the panel; Esc closes
// without submitting - the input widget's blur-submit is deliberately left
// unwired for that reason. Opening prefills "/" (D7): the mandatory prefix
// becomes the default experience and the open-frame key race (the "/" that
// opened the panel never reaches the field) is absorbed. Code-built shell like
// WidgetTestUI - the layout schema has no text/input elements.
public class ChatPanelUI : UIBehavior
{
    private const float WrapWidth = 900f;      // history bar / input row width cap
    private const float LeftMargin = 4f;
    private const float InputBottomY = 130f;   // clears the hotbar row
    private const float InputHeight = 30f;
    private const float HistoryBaseY = InputBottomY + InputHeight + 8f;
    private const float TopPad = 10f;          // gap kept above the top history line
    private const float LineMinHeight = 24f;
    private const float LinePadX = 6f;         // matches the input widget's text inset
    private const float ScrollStep = 0.1f;     // wheel axis units per history entry
    private static readonly Color BarColor = new(0f, 0f, 0f, 0.35f);
    private static readonly Color PlainColor = Color.white;
    private static readonly Color ErrorColor = new(1f, 1f / 3f, 1f / 3f);   // vanilla error red

    private UITextInputWidget input;
    private TMP_InputField inputField;
    private readonly List<Line> lines = new();
    private Material shadowMaterial;
    private int shownVersion = -1;
    private int scrollOffset;                  // entries above the newest visible window (P2)
    private int shownScrollOffset = -1;
    private float scrollAccum;
    private float shownHeight = -1f;
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
        scrollOffset = 0;   // vanilla: every open starts at the bottom
        scrollAccum = 0f;
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
        HandleScroll();
        HandleSubmit();
        RefreshHistory();
    }

    // Wheel browsing (P2, vanilla chat-screen semantics): one entry per notch,
    // up = older. The offset clamps against the current history, so a wheel
    // motion at either end is a no-op with no hidden accumulation.
    private void HandleScroll()
    {
        float axis = Input.GetAxis("Mouse ScrollWheel");
        if(axis == 0f)return;
        scrollAccum += axis;
        while(scrollAccum >= ScrollStep){ scrollAccum -= ScrollStep; ScrollBy(1); }
        while(scrollAccum <= -ScrollStep){ scrollAccum += ScrollStep; ScrollBy(-1); }
    }

    private void ScrollBy(int delta)
    {
        int count = MirrorSync.Instance.ChatLogMirror?.Lines?.Length ?? 0;
        scrollOffset = Mathf.Clamp(scrollOffset + delta, 0, Mathf.Max(0, count - VisibleLineCount()));
    }

    // How many entries fit above the input row: the visible window is bounded
    // by the canvas height, the rest of the log stays reachable by scrolling.
    private int VisibleLineCount()
    {
        float height = ((RectTransform)transform).rect.height;
        return Mathf.Max(1, Mathf.FloorToInt((height - HistoryBaseY - TopPad) / LineMinHeight));
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

    // Redraw gate: the mirror snapshots on the render frame, so a rebuild
    // only happens when a command wrote to the log, the wheel moved the
    // window or the canvas resized. Newest entry sits lowest (vanilla); bars
    // hug the text width; the window shows the newest entries at scrollOffset
    // 0 and slides into history as the wheel moves up.
    private void RefreshHistory()
    {
        var mirror = MirrorSync.Instance.ChatLogMirror;
        if(mirror == null)return;
        float canvasHeight = ((RectTransform)transform).rect.height;
        if(mirror.Version == shownVersion && scrollOffset == shownScrollOffset
            && Mathf.Approximately(canvasHeight, shownHeight))return;
        int count = mirror.Lines?.Length ?? 0;
        // Scrolled into history and new entries arrive: shift the window with
        // the log so the viewed content stays put instead of jumping away.
        if(scrollOffset > 0 && shownVersion >= 0 && mirror.Version > shownVersion)
            scrollOffset += mirror.Version - shownVersion;
        shownVersion = mirror.Version;
        shownHeight = canvasHeight;
        int visible = VisibleLineCount();
        scrollOffset = Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, count - visible));
        shownScrollOffset = scrollOffset;
        int end = count - scrollOffset;
        int shown = Mathf.Min(visible, end);
        int start = end - shown;
        float y = HistoryBaseY;
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
        // Rich text off, kept deliberately (P2 review): TMP 3.0.7 has no
        // HTML-entity escape for user input and noparse wrapping has its own
        // breakout string, so the parser stays out of the user-input path
        // entirely; vertex-color per-line styling already covers success/error.
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
