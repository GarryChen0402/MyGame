using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Registered in-game pause page (ESC, PlayerInputHandler): vanilla single-
// player semantics - the page freezes the 20Hz logic while on screen (the
// same GameLoopDriver.PauseLogic gate the enter-world sequence uses, which
// also zeroes GameClock.Alpha so render interpolation goes still) and hides
// the game HUD; both revert when the page closes. The dedicated input
// context (minecraft:pause_input_handler) owns Esc-closing and keeps JEI's
// world-panel follow and every other action whitelist out while paused (the
// same name contract as the chat panel).
// Two exits: Back to Game (plain CloseUI) and Save and Quit to Title
// (GameEntryController.ReturnToMainMenu - save, world teardown, reopen menu).
public class PauseMenuUI : UIBehavior
{
    private const float TitleY = 100f;
    private const float ButtonY = 22f;
    private const float ButtonW = 400f, ButtonH = 36f;
    private const float ButtonGap = 8f;
    // Translucent: unlike the menu's opaque backdrop, the frozen world stays
    // visible behind the pause page (vanilla).
    private static readonly Color BackdropColor = new(0f, 0f, 0f, 0.6f);
    private static readonly Color TitleColor = Color.white;

    public static readonly UIDefinition pauseMenuUIDefinition = new()
    {
        modId = "minecraft",
        name = "pause_menu",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:pause_input_handler",
        OpenWithPlayerInventory = false,
        Factory = () =>
        {
            var go = new GameObject("Pause Menu", typeof(RectTransform));
            go.AddComponent<PauseMenuUI>();
            return go;
        }
    };

    private void Awake()
    {
        // Factory GO comes with a RectTransform: stretch it fullscreen.
        var rootRt = (RectTransform)transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var backdropRt = FullRect("Backdrop", transform);
        var backdropImage = backdropRt.gameObject.AddComponent<Image>();
        backdropImage.color = BackdropColor;

        CenterText("Txt_Title", "Game Menu", transform, new Vector2(0f, TitleY));

        var back = MakeButton("Btn_BackToGame", "Back to Game", transform,
            new Vector2(0f, ButtonY));
        back.OnClick.AddListener(() => UIManager.Instance?.CloseUI());
        var quit = MakeButton("Btn_SaveAndQuit", "Save and Quit to Title", transform,
            new Vector2(0f, ButtonY - ButtonH - ButtonGap));
        quit.OnClick.AddListener(() => GameEntryController.Instance?.ReturnToMainMenu());
    }

    // True pause while the page is on screen. Close (Esc / Back to Game) runs
    // the inverse through UIManager.CloseUI -> OnDisable; the Save-and-Quit
    // path re-freezes in the same synchronous stack (ReturnToMainMenu) before
    // a tick can run, so the brief resume is never observable. On the first
    // open OnEnable runs at AddComponent (the factory GO is active), which is
    // equally correct: the page is opening right then.
    private void OnEnable()
    {
        GameLoopDriver.PauseLogic = true;
        UIManager.Instance?.CloseGameHUD();
    }

    private void OnDisable()
    {
        GameLoopDriver.PauseLogic = false;
        UIManager.Instance?.OpenGameHUD();
    }

    // ---- widget helpers (MenuUI's absolute-placement style) ----

    private static RectTransform FullRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static GameObject CenterText(string name, string text, Transform parent,
        Vector2 pos)
    {
        var go = UITextWidget.CreateNewText(text, parent.gameObject);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(600f, 60f);
        rt.anchoredPosition = pos;
        var widget = go.GetComponent<UITextWidget>();
        widget.SetFontSize(48f);
        widget.SetColor(TitleColor);
        widget.SetAlignment(TextAlignmentOptions.Center);
        return go;
    }

    private static UIButtonWidget MakeButton(string name, string label, Transform parent,
        Vector2 pos)
    {
        var go = UIButtonWidget.CreateNewButton(label, null, parent.gameObject);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(ButtonW, ButtonH);
        rt.anchoredPosition = pos;
        var widget = go.GetComponent<UIButtonWidget>();
        widget.SetFontSize(22f);
        return widget;
    }
}
