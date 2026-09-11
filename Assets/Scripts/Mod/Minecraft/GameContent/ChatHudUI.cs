using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Transient chat HUD (vanilla in-world chat display, 2026-09-11 rework):
// newly logged chat lines appear bottom-left above the hotbar, linger ~10
// seconds and fade out over the last second; the newest sits lowest. Bare
// white text with the vanilla hard drop shadow - the translucent bars belong
// to the chat panel's history, not to this HUD. While the chat panel is open
// the HUD yields (lines freeze hidden; the panel shows the full history) and
// resumes when the panel closes. Reads the ChatLogMirror like the panel does;
// message ages are render-side state, the shared log carries none.
public class ChatHudUI : UIBehavior
{
    private const int MaxLines = 10;        // vanilla default visible count
    private const float Lifetime = 10f;     // vanilla: 200 ticks
    private const float FadeTime = 1f;      // vanilla fades the last 20 ticks
    private const float BaseY = 168f;       // same baseline as the chat panel history
    private const float LeftMargin = 4f;
    private const float WrapWidth = 900f;
    private const float LineMinHeight = 24f;
    private const float LinePadX = 6f;
    private static readonly Color PlainColor = Color.white;
    private static readonly Color ErrorColor = new(1f, 1f / 3f, 1f / 3f);   // vanilla error red

    private class Line
    {
        public RectTransform root;
        public TextMeshProUGUI text;
        public float Height;
        public float Remaining;
        public bool IsError;
        public bool InUse;
    }

    private readonly List<Line> pool = new();     // created line objects (recycled)
    private readonly List<Line> alive = new();    // counting down, arrival order
    private Material shadowMaterial;
    private int lastVersion = -1;
    private bool suppressed;

    private void Awake()
    {
        // Full-screen transparent root under the HUD root; children anchor to
        // the screen's bottom left (the chat panel's shell, minus the input).
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void Update()
    {
        // Panel open: hide and freeze - the countdown is remaining-time based,
        // so skipping the tick pauses it (vanilla's screen-open behaviour,
        // including the replay of what arrived meanwhile once it closes).
        if(ChatPanelUI.IsOpen)
        {
            if(!suppressed)
            {
                suppressed = true;
                foreach(var line in alive)line.root.gameObject.SetActive(false);
            }
            return;
        }
        suppressed = false;

        Age(Time.deltaTime);
        ConsumeNew();
        Layout();
    }

    private void Age(float dt)
    {
        for(int i = alive.Count - 1; i >= 0; i--)
        {
            var line = alive[i];
            line.Remaining -= dt;
            if(line.Remaining > 0f)continue;
            line.root.gameObject.SetActive(false);
            line.InUse = false;
            alive.RemoveAt(i);
        }
    }

    // Version-delta consumption: entries appended since the last look are the
    // new messages (the delta also covers ring eviction at capacity 50). The
    // first look only latches the version - the log is process-lifetime, so
    // pre-HUD history must not flash on world entry.
    private void ConsumeNew()
    {
        var mirror = MirrorSync.Instance.ChatLogMirror;
        if(mirror == null || mirror.Version == lastVersion)return;
        int delta = lastVersion < 0 ? 0 : mirror.Version - lastVersion;
        lastVersion = mirror.Version;
        int count = mirror.Lines?.Length ?? 0;
        for(int i = Mathf.Max(0, count - delta); i < count; i++)
            AddLine(mirror.Lines[i], mirror.Errors[i]);
    }

    private void AddLine(string text, bool isError)
    {
        var line = Obtain();
        line.InUse = true;
        line.IsError = isError;
        line.Remaining = Lifetime;
        // A recycled line may still be inactive; TMP skips mesh generation on
        // inactive objects, so activate before measuring (the panel's order).
        line.root.gameObject.SetActive(true);
        line.text.text = text ?? "";
        // Measure once - a line's text never changes afterwards, so the
        // per-frame placement pass below stays cheap.
        line.root.sizeDelta = new Vector2(WrapWidth, LineMinHeight);
        line.text.ForceMeshUpdate();
        float width = Mathf.Clamp(line.text.preferredWidth + LinePadX * 2f, 24f, WrapWidth);
        float height = Mathf.Max(LineMinHeight, line.text.preferredHeight + 2f);
        line.root.sizeDelta = new Vector2(width, height);
        line.Height = height;
        alive.Add(line);
    }

    // Newest lowest (vanilla): walk the alive list backwards from the bottom
    // baseline. Lines past the visible cap stay alive but hidden, so they
    // still expire on schedule.
    private void Layout()
    {
        float y = BaseY;
        int shown = 0;
        for(int k = alive.Count - 1; k >= 0; k--)
        {
            var line = alive[k];
            if(shown >= MaxLines)
            {
                line.root.gameObject.SetActive(false);
                continue;
            }
            shown++;
            line.root.gameObject.SetActive(true);
            var color = line.IsError ? ErrorColor : PlainColor;
            color.a = Mathf.Clamp01(line.Remaining / FadeTime);
            line.text.color = color;
            line.root.anchoredPosition = new Vector2(LeftMargin, y);
            y += line.Height;
        }
    }

    private Line Obtain()
    {
        foreach(var line in pool)
            if(!line.InUse)return line;
        var created = CreateLine(pool.Count);
        pool.Add(created);
        return created;
    }

    private Line CreateLine(int index)
    {
        var go = new GameObject($"Line {index}", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(WrapWidth, LineMinHeight);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(LinePadX, 0f);
        textRt.offsetMax = new Vector2(-LinePadX, 0f);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.richText = false;   // user input must never reach the rich-text parser
        text.alignment = TextAlignmentOptions.TopLeft;
        text.fontSize = 20;
        text.overflowMode = TextOverflowModes.Overflow;
        ApplyShadow(text);
        return new Line { root = rt, text = text };
    }

    // Same underlay shadow as the chat panel: one material per HUD, shared by
    // all of its lines.
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

    public static UIDefinition chatHudUIDefinition = new()
    {
        modId = "minecraft",
        name = "chat_hud",
        Kind = UIKind.HUD,
        Factory = () =>
        {
            var go = new GameObject("Chat HUD", typeof(ChatHudUI));
            return go;
        }
    };
}
