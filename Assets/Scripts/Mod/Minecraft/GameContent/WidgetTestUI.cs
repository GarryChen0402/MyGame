using System.Collections.Generic;
using UnityEngine;

// Widget smoke-test panel (UI 组件化重构设计方案 §3.4 controls): a tab
// control hosting the icon / text / integer-input / button widgets on page 1
// and an input + two buttons on page 2. The buttons grant the typed amount of
// dirt / cobblestone / stone straight into the player inventory - a visible
// end-to-end check of widget wiring without touching container UIs. Opened by
// minecraft:open_widget_test (default T).
public class WidgetTestUI : UIBehavior
{
    private void Awake()
    {
        var rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(620, 340);
        rt.localPosition += new Vector3(0, 180, 0);   // clear the shared backpack strip below
        var bg = UIWidgetBackground.CreateNewBackground();
        bg.transform.SetParent(transform, false);
        var tabGo = UITabWidget.CreateNewTab(gameObject);
        var tab = tabGo.GetComponent<UITabWidget>();
        ((RectTransform)tabGo.transform).sizeDelta = new Vector2(620, 340);
        tab.AddTab("page1", "Page 1", BuildPage1);
        tab.AddTab("page2", "Page 2", BuildPage2);
        tab.AddTab("page3", "Page 3", BuildPage3);
    }

    // ---- page 1: icon + text + integer input + one give-button ----

    private static GameObject BuildPage1()
    {
        var page = NewPage("Page 1");

        var iconGo = UIIconWidget.CreateNewIcon(Resources.Load<Sprite>("Textures/UI/slot"), page);
        Place(iconGo, new Vector2(-170, 60), new Vector2(64, 64));

        var textGo = UITextWidget.CreateNewText("Widget test - grant items below", page);
        Place(textGo, new Vector2(30, 70), new Vector2(340, 40));

        var inputGo = UIIntegerInputWidget.CreateNewIntegerInput(64, page);
        Place(inputGo, new Vector2(-90, -40), new Vector2(140, 36));
        var input = inputGo.GetComponent<UIIntegerInputWidget>();

        var giveGo = UIButtonWidget.CreateNewButton("Give Dirt", null, page);
        Place(giveGo, new Vector2(60, -40), new Vector2(130, 40));
        giveGo.GetComponent<UIButtonWidget>().OnClick.AddListener(
            () => ContainerCommandProcessor.Instance.GiveItem("minecraft:dirt", input.GetValue()));

        return page;
    }

    // ---- page 2: integer input + cobblestone/stone give-buttons ----

    private static GameObject BuildPage2()
    {
        var page = NewPage("Page 2");

        var inputGo = UIIntegerInputWidget.CreateNewIntegerInput(64, page);
        Place(inputGo, new Vector2(0, 50), new Vector2(140, 36));
        var input = inputGo.GetComponent<UIIntegerInputWidget>();

        var stoneGo = UIButtonWidget.CreateNewButton("Give Stone", null, page);
        Place(stoneGo, new Vector2(-90, -40), new Vector2(150, 40));
        stoneGo.GetComponent<UIButtonWidget>().OnClick.AddListener(
            () => ContainerCommandProcessor.Instance.GiveItem("minecraft:stone", input.GetValue()));

        var cobbleGo = UIButtonWidget.CreateNewButton("Give Cobblestone", null, page);
        Place(cobbleGo, new Vector2(90, -40), new Vector2(150, 40));
        cobbleGo.GetComponent<UIButtonWidget>().OnClick.AddListener(
            () => ContainerCommandProcessor.Instance.GiveItem("minecraft:cobblestone", input.GetValue()));

        return page;
    }

    // ---- page 3: dropdown + free-text input widgets (new-control smoke test) ----

    private static GameObject BuildPage3()
    {
        var page = NewPage("Page 3");

        var headGo = UITextWidget.CreateNewText("Widget test - dropdown & text input", page);
        Place(headGo, new Vector2(0, 115), new Vector2(460, 30));

        // Dropdown: picking an option fires OnSelectionChanged and mirrors into
        // the label on the right.
        var dropGo = UIDropdownWidget.CreateNewDropdown("Select fruit...", page);
        Place(dropGo, new Vector2(-200, 55), new Vector2(180, 32));
        var drop = dropGo.GetComponent<UIDropdownWidget>();
        drop.SetOptions(new List<string> { "Apple", "Banana", "Cherry", "Durian", "Elderberry", "Fig" });
        var pickedLabel = UITextWidget.CreateNewText("(none)", page);
        Place(pickedLabel, new Vector2(60, 55), new Vector2(220, 30));
        var picked = pickedLabel.GetComponent<UITextWidget>();
        drop.OnSelectionChanged.AddListener(_ =>
            picked.SetText($"picked: {drop.SelectedLabel}"));

        // Free text input: submits on Enter or focus loss (OnSubmit), shown in
        // the label on the right.
        var inputGo = UITextInputWidget.CreateNewTextInput("", "type text, Enter / blur submits", page);
        Place(inputGo, new Vector2(-200, -5), new Vector2(180, 32));
        var input = inputGo.GetComponent<UITextInputWidget>();
        var textLabel = UITextWidget.CreateNewText("", page);
        Place(textLabel, new Vector2(60, -5), new Vector2(220, 30));
        var textOut = textLabel.GetComponent<UITextWidget>();
        input.OnSubmit.AddListener(_ => textOut.SetText($"last: {input.GetText()}"));

        // Toggles the host-driven red invalid frame of the text input.
        var invalidGo = UIButtonWidget.CreateNewButton("Mark Invalid", null, page);
        Place(invalidGo, new Vector2(-200, -70), new Vector2(180, 34));
        bool invalid = false;
        invalidGo.GetComponent<UIButtonWidget>().OnClick.AddListener(() =>
        {
            invalid = !invalid;
            input.SetInvalid(invalid);
            invalidGo.GetComponent<UIButtonWidget>().SetText(invalid ? "Mark Valid" : "Mark Invalid");
        });
        var invalidHint = UITextWidget.CreateNewText("toggles the red invalid frame", page);
        Place(invalidHint, new Vector2(60, -70), new Vector2(220, 30));

        return page;
    }

    private static GameObject NewPage(string name)
    {
        var page = new GameObject(name, typeof(RectTransform));
        var prt = (RectTransform)page.transform;
        prt.anchorMin = Vector2.zero;   // stretch over the tab widget's content area
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        return page;
    }

    private static void Place(GameObject go, Vector2 position, Vector2 size)
    {
        go.transform.localPosition = position;
        ((RectTransform)go.transform).sizeDelta = size;
    }

    public static UIDefinition widgetTestUIDefinition = new()
    {
        modId = "minecraft",
        name = "widget_test",
        Kind = UIKind.SinglePanel,
        InputHandlerId = "minecraft:ui_input_handler",
        OpenWithPlayerInventory = true,   // backpack strip visible so grants are observable on the spot
        Factory = () =>
        {
            var go = new GameObject("Widget Test UI", typeof(WidgetTestUI));
            return go;
        }
    };
}
