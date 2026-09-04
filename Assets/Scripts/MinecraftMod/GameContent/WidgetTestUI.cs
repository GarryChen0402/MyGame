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
        giveGo.GetComponent<UIButtonWidget>().OnClick.AddListener(() => GiveItems("minecraft:dirt", input.GetValue()));

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
        stoneGo.GetComponent<UIButtonWidget>().OnClick.AddListener(() => GiveItems("minecraft:stone", input.GetValue()));

        var cobbleGo = UIButtonWidget.CreateNewButton("Give Cobblestone", null, page);
        Place(cobbleGo, new Vector2(90, -40), new Vector2(150, 40));
        cobbleGo.GetComponent<UIButtonWidget>().OnClick.AddListener(() => GiveItems("minecraft:cobblestone", input.GetValue()));

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

    // Grants `amount` of the named item in MaxStack chunks; stops when the
    // backpack cannot hold more. Returns how much actually landed.
    private static int GiveItems(string fullName, int amount)
    {
        if(amount <= 0)return 0;
        var rs = ResourceSystem.Instance;
        if(!rs.ItemDefinitions.TryGetResourceWithFullName(fullName, out var def))return 0;
        if(!rs.ItemDefinitions.TryGetNumberId(fullName, out ushort id))return 0;
        var inventory = Player.Instance?.inventory;
        if(inventory == null)return 0;

        int remaining = amount;
        while(remaining > 0)
        {
            int piece = Mathf.Min(remaining, def.MaxStack);
            var stack = new ItemStack { itemId = id, amount = piece };
            if(!inventory.TryAddItemStack(stack))break;   // full: grant what fits, drop the rest
            remaining -= piece;
        }
        int granted = amount - remaining;
        Debug.Log($"WidgetTest: granted {granted}/{amount} {def.FullName}");
        return granted;
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
