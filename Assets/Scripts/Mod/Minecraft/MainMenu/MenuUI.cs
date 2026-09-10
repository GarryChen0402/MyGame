using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Registered main-menu panel (design §3.5/§5): a UIBehavior whose whole tree
// is code-built in Awake from the Widget octet (no scene nodes, no name-based
// bindings - the factory GO is the root). SinglePanel def means it shares the
// container-panel lifecycle: OpenUI parents it under SinglePanelRoot, caches
// the instance, pushes its input handler and unlocks the cursor. Unlike
// container UIs it carries no BE session (no PanelData packet) and has no Esc-close:
// the only close path is EnterWorld's CloseUI.
//
// Pages: MainPage (v1: Single Player / Quit only), WorldSelectPage (rows
// rebuilt on every entry), CreateWorldPage (name + optional seed validation).
public class MenuUI : UIBehavior
{
    // ---- layout constants (canvas scale 1, px from the screen center) ----
    private const float TitleY = 230f;
    private const float HintY = 60f;
    private const float MainButtonGap = 80f;
    private const float MainButtonW = 300f, MainButtonH = 36f;
    private const float RowStartY = 130f, RowH = 46f, RowGap = 6f;
    private const int MaxVisibleRows = 7;               // until Btn_NewWorld
    private const float RowNameX = 150f, RowNameW = 470f;
    private const float RowTimeX = 640f, RowTimeW = 330f;
    private const float RowButtonW = 88f, RowButtonH = 30f;
    private const float NewWorldY = -260f;
    private const float CreatePanelW = 480f, CreatePanelH = 300f;
    private const float FieldW = 340f, FieldH = 36f;
    private const float ConfirmY = -120f;
    private const float DeleteConfirmSeconds = 3f;

    private static readonly Color BackdropColor = new(0.13f, 0.15f, 0.19f, 1f);
    private static readonly Color TitleColor = new(0.92f, 0.92f, 0.92f, 1f);
    private static readonly Color HintColor = new(1f, 0.3f, 0.3f, 1f);
    private static readonly Color VersionColor = new(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color RowNameColor = new(0.92f, 0.92f, 0.92f, 1f);
    private static readonly Color RowTimeColor = new(0.65f, 0.65f, 0.65f, 1f);
    private static readonly Color EmptyColor = new(0.75f, 0.75f, 0.75f, 1f);
    private static readonly Color CreateErrorColor = new(0.75f, 0.1f, 0.1f, 1f);
    private static readonly Color ButtonNormalColor = new(1f, 1f, 1f, 0.8f);
    private static readonly Color DeleteArmedColor = new(0.8f, 0.15f, 0.15f, 0.9f);

    public static readonly UIDefinition mainMenuUIDefinition = new()
    {
        modId = "minecraft",
        name = "main_menu",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:menu_input_handler",
        OpenWithPlayerInventory = false,
        Factory = () =>
        {
            var go = new GameObject("Main Menu", typeof(RectTransform));
            go.AddComponent<MenuUI>();
            return go;
        }
    };

    private GameObject mainPage = null;
    private GameObject worldSelectPage = null;
    private GameObject createWorldPage = null;
    private GameObject worldListRoot = null;
    private GameObject txtEmpty = null;
    private UITextWidget txtHint = null;
    private UITextInputWidget inputName = null;
    private UITextInputWidget inputSeed = null;
    private UITextWidget txtCreateError = null;

    // Two-click delete state (rows are rebuilt each entry, so the armed button
    // is only meaningful while its row still exists).
    private UIButtonWidget armedDeleteButton = null;
    private string armedDeleteFolder = null;
    private float armedDeleteDeadline = 0f;

    private void Awake()
    {
        // Factory GO comes with a RectTransform: stretch it fullscreen.
        var rootRt = (RectTransform)transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // ---- backdrop & shared texts (built first: later children draw on top) ----
        var backdropRt = FullRect("Backdrop", transform);
        var backdropImage = backdropRt.gameObject.AddComponent<Image>();
        backdropImage.color = BackdropColor;

        CenterText("Txt_Title", "Minecraft", transform, new Vector2(0f, TitleY),
            88f, TitleColor, 900f, 90f);
        CenterText("Txt_Version", "v0.1-test", transform, new Vector2(0f, 0f),
            14f, VersionColor, 400f, 24f, bottomRight: true);

        // ---- pages ----
        mainPage = FullRect("MainPage", transform).gameObject;
        worldSelectPage = FullRect("WorldSelectPage", transform).gameObject;
        worldSelectPage.SetActive(false);
        createWorldPage = FullRect("CreateWorldPage", transform).gameObject;
        createWorldPage.SetActive(false);

        BuildMainPage(mainPage.transform);
        BuildWorldSelectPage(worldSelectPage.transform);
        BuildCreateWorldPage(createWorldPage.transform);

        // Shared hint line must stay the topmost sibling so no page covers it.
        var hintGo = CenterText("Txt_Hint", "", transform, new Vector2(0f, HintY),
            18f, HintColor, 900f, 30f);
        txtHint = hintGo.GetComponent<UITextWidget>();
    }

    // ---- page building ----

    private void BuildMainPage(Transform page)
    {
        var singlePlayer = MakeButton("Btn_SinglePlayer", "Single Player", page,
            new Vector2(0f, MainButtonGap / 2f), new Vector2(MainButtonW, MainButtonH), 22f);
        singlePlayer.OnClick.AddListener(() => ShowWorldSelectPage());
        var quit = MakeButton("Btn_Quit", "Quit", page,
            new Vector2(0f, -MainButtonGap / 2f), new Vector2(MainButtonW, MainButtonH), 22f);
        quit.OnClick.AddListener(() =>
        {
#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        });
    }

    private void BuildWorldSelectPage(Transform page)
    {
        var back = CornerButton("Btn_BackToMain", "< Back", page,
            new Vector2(30f, -30f), new Vector2(110f, 34f));
        back.OnClick.AddListener(() => ShowMainPage());

        worldListRoot = FullRect("WorldListRoot", page).gameObject;
        var emptyGo = CenterText("Txt_Empty", "No worlds yet - create one", page,
            new Vector2(0f, -40f), 22f, EmptyColor, 700f, 30f);
        txtEmpty = emptyGo;
        txtEmpty.SetActive(false);

        var newWorld = MakeButton("Btn_CreateWorld", "New World", page,
            new Vector2(0f, NewWorldY), new Vector2(MainButtonW, MainButtonH), 22f);
        newWorld.OnClick.AddListener(() => ShowCreateWorldPage());
    }

    private void BuildCreateWorldPage(Transform page)
    {
        var back = CornerButton("Btn_BackFromCreate", "Back", page,
            new Vector2(30f, -30f), new Vector2(110f, 34f));
        back.OnClick.AddListener(() => ShowWorldSelectPage());

        // Decorative focus panel: nine-slice behind the inputs.
        var panelBg = UIWidgetBackground.CreateNewBackground();
        panelBg.transform.SetParent(page, false);
        var panelRt = (RectTransform)panelBg.transform;
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(CreatePanelW, CreatePanelH);

        CenterText("Txt_PanelTitle", "Create New World", page, new Vector2(0f, 110f),
            26f, Color.black, 400f, 34f);

        var nameGo = MakeInput("Input_WorldName", "World name", page, new Vector2(0f, 50f));
        inputName = nameGo;
        var seedGo = MakeInput("Input_Seed", "Seed (empty = random)", page, new Vector2(0f, 0f));
        inputSeed = seedGo;

        var errorGo = CenterText("Txt_CreateError", "", page, new Vector2(0f, -65f),
            17f, CreateErrorColor, 440f, 24f);
        txtCreateError = errorGo.GetComponent<UITextWidget>();

        var confirm = MakeButton("Btn_ConfirmCreate", "Create & Play", page,
            new Vector2(0f, ConfirmY), new Vector2(220f, 34f), 20f);
        confirm.OnClick.AddListener(OnConfirmCreateClicked);
    }

    // ---- page switching ----

    public override void SetData(object data)
    {
        // UICache reopen (or first open): always land on the main page with
        // fresh state - no page state survives a close.
        ShowMainPage();
    }

    private void ShowPage(GameObject page)
    {
        mainPage.SetActive(page == mainPage);
        worldSelectPage.SetActive(page == worldSelectPage);
        createWorldPage.SetActive(page == createWorldPage);
        txtHint.SetText("");
    }

    private void ShowMainPage()
    {
        ShowPage(mainPage);
        DisarmDelete();
    }

    private void ShowWorldSelectPage()
    {
        ShowPage(worldSelectPage);
        DisarmDelete();
        RebuildWorldList();
    }

    private void ShowCreateWorldPage()
    {
        ShowPage(createWorldPage);
        DisarmDelete();
        inputName.SetText("");
        inputSeed.SetText("");
        inputName.SetInvalid(false);
        inputSeed.SetInvalid(false);
        txtCreateError.SetText("");
    }

    // ---- world list ----

    private void RebuildWorldList()
    {
        foreach(Transform child in worldListRoot.transform)
        {
            child.gameObject.SetActive(false);   // no one-frame ghost rows
            Destroy(child.gameObject);
        }

        var slots = SaveSlots.ListSlots();
        txtEmpty.SetActive(slots.Count == 0);
        int shown = 0;
        foreach(var slot in slots)
        {
            if(shown >= MaxVisibleRows)
            {
                txtHint.SetText("Too many worlds - only the latest 7 are listed");
                break;
            }
            AddRow(slot, shown);
            shown++;
        }
    }

    private void AddRow(WorldSlotInfo slot, int index)
    {
        float cy = RowStartY - index * (RowH + RowGap);
        LeftText("Txt_Name", slot.Name, worldListRoot, new Vector2(RowNameX, cy),
            20f, RowNameColor, new Vector2(RowNameW, 30f));
        string timeText = SlotTimeText(slot);
        if(timeText.Length > 0)
            LeftText("Txt_Time", timeText, worldListRoot, new Vector2(RowTimeX, cy),
                14f, RowTimeColor, new Vector2(RowTimeW, 24f));

        string folder = slot.Folder;
        var enter = EdgeButton("Btn_Enter", "Enter", worldListRoot,
            new Vector2(-180f, cy), new Vector2(RowButtonW, RowButtonH), 16f, right: true);
        enter.OnClick.AddListener(() => GameEntryController.Instance?.EnterWorld(folder));

        var delete = EdgeButton("Btn_Delete", "Delete", worldListRoot,
            new Vector2(-84f, cy), new Vector2(RowButtonW, RowButtonH), 16f, right: true);
        delete.OnClick.AddListener(() => OnDeleteClicked(delete, folder));
    }

    private static string SlotTimeText(WorldSlotInfo slot)
    {
        if(slot.LastPlayedTime != DateTime.MinValue)
            return "Last played " + slot.LastPlayedTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        if(slot.CreatedTime != DateTime.MinValue)
            return "Created " + slot.CreatedTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        return "";
    }

    // Two-click confirm (design §5.3): first click arms the row (red "Delete?"),
    // a second click on the same button deletes; another row's first click
    // re-arms; the arm times out after DeleteConfirmSeconds (Update-driven,
    // which only runs while the panel is open).
    private void OnDeleteClicked(UIButtonWidget button, string folder)
    {
        if(armedDeleteButton == button && armedDeleteFolder == folder)
        {
            bool ok = SaveSlots.DeleteSlot(folder);
            DisarmDelete();
            if(!ok)txtHint.SetText("Failed to delete the world (see console)");
            RebuildWorldList();
            return;
        }
        DisarmDelete();
        armedDeleteButton = button;
        armedDeleteFolder = folder;
        armedDeleteDeadline = Time.unscaledTime + DeleteConfirmSeconds;
        button.SetText("Delete?");
        button.SetColor(DeleteArmedColor);
    }

    private void DisarmDelete()
    {
        if(armedDeleteButton != null)
        {
            armedDeleteButton.SetText("Delete");
            armedDeleteButton.SetColor(ButtonNormalColor);
        }
        armedDeleteButton = null;
        armedDeleteFolder = null;
    }

    private void Update()
    {
        if(armedDeleteButton != null && Time.unscaledTime >= armedDeleteDeadline)
            DisarmDelete();
    }

    // ---- create world (validation chain, design §5.4) ----

    private void OnConfirmCreateClicked()
    {
        string name = inputName.GetText()?.Trim() ?? "";
        string rawSeed = inputSeed.GetText()?.Trim() ?? "";
        txtCreateError.SetText("");
        inputName.SetInvalid(false);
        inputSeed.SetInvalid(false);

        string error = null;
        int? seed = null;
        if(name.Length == 0)
        {
            error = "World name is required";
            inputName.SetInvalid(true);
        }
        else if(SaveSlots.FolderExists(SaveSlots.SanitizeFolderName(name)))
        {
            error = "A world with that name already exists";
            inputName.SetInvalid(true);
        }

        if(rawSeed.Length > 0)
        {
            if(int.TryParse(rawSeed, out int parsed))seed = parsed;
            else
            {
                if(error == null)error = "Seed must be an integer (empty = random)";
                inputSeed.SetInvalid(true);
            }
        }

        if(error != null)
        {
            txtCreateError.SetText(error);
            return;
        }
        var info = SaveSlots.CreateWorld(name, seed);
        if(info == null)
        {
            txtCreateError.SetText("Failed to create the world (see console)");
            return;
        }
        GameEntryController.Instance?.EnterWorld(info.Folder);
    }

    // ---- widget helpers (absolute placement; widgets default to center anchor) ----

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
        Vector2 pos, float fontSize, Color color, float width, float height,
        bool bottomRight = false)
    {
        var go = UITextWidget.CreateNewText(text, parent.gameObject);
        go.name = name;
        var rt = (RectTransform)go.transform;
        if(bottomRight)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-20f, 20f);
        }
        else
        {
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = pos;
        }
        var widget = go.GetComponent<UITextWidget>();
        widget.SetFontSize(fontSize);
        widget.SetColor(color);
        widget.SetAlignment(bottomRight ? TextAlignmentOptions.Right : TextAlignmentOptions.Center);
        return go;
    }

    private static GameObject LeftText(string name, string text, GameObject parent,
        Vector2 pos, float fontSize, Color color, Vector2 size)
    {
        var go = UITextWidget.CreateNewText(text, parent);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var widget = go.GetComponent<UITextWidget>();
        widget.SetFontSize(fontSize);
        widget.SetColor(color);
        widget.SetAlignment(TextAlignmentOptions.Left);
        return go;
    }

    private static UIButtonWidget MakeButton(string name, string label, Transform parent,
        Vector2 pos, Vector2 size, float fontSize)
    {
        var go = UIButtonWidget.CreateNewButton(label, null, parent.gameObject);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var widget = go.GetComponent<UIButtonWidget>();
        widget.SetFontSize(fontSize);
        return widget;
    }

    private static UIButtonWidget CornerButton(string name, string label, Transform parent,
        Vector2 offset, Vector2 size)
    {
        var widget = MakeButton(name, label, parent, Vector2.zero, size, 20f);
        var rt = (RectTransform)widget.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);   // top-left corner
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = offset;
        return widget;
    }

    private static UIButtonWidget EdgeButton(string name, string label, GameObject parent,
        Vector2 pos, Vector2 size, float fontSize, bool right)
    {
        var go = UIButtonWidget.CreateNewButton(label, null, parent);
        go.name = name;
        var rt = (RectTransform)go.transform;
        if(right)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
        }
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var widget = go.GetComponent<UIButtonWidget>();
        widget.SetFontSize(fontSize);
        return widget;
    }

    private static UITextInputWidget MakeInput(string name, string placeholder,
        Transform parent, Vector2 pos)
    {
        var go = UITextInputWidget.CreateNewTextInput("", placeholder, parent.gameObject);
        go.name = name;
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(FieldW, FieldH);
        rt.anchoredPosition = pos;
        return go.GetComponent<UITextInputWidget>();
    }
}
