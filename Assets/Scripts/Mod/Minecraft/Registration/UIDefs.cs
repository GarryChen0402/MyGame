using UnityEngine;

// Registration helper (E6, D3 of Docs/可视化UI布局编辑器-实施文档.md): folds
// the repeated SinglePanel UIDefinition boilerplate into one call and
// resolves the panel layout at registration time - JSON asset first, the
// C# static layout as fallback (PanelLayoutAssets.Load) - so opening a panel
// never touches the disk. Lives in the Minecraft mod's registration layer;
// promote to Core only when another mod (JEI et al.) needs it.
public static class UIDefs
{
    public static UIDefinition SinglePanel<TPanel>(string name, string displayName,
        string layoutPath, PanelLayout fallback, PanelDescriptor panel) where TPanel : UIBehavior
    {
        panel.Layout = PanelLayoutAssets.Load(layoutPath, fallback);
        return new UIDefinition
        {
            modId = "minecraft",
            name = name,
            Kind = UIKind.SinglePanel,
            InputHandlerId = "minecraft:ui_input_handler",
            OpenWithPlayerInventory = true,
            Panel = panel,
            Factory = () => new GameObject(displayName, typeof(TPanel))
        };
    }
}
