public partial class Minecraft
{
    // ---- H: item behaviors ----
    private void RegisterItemBehaviors()
    {
        ResourceSystem.Instance.ItemBehaviors.Register(new UniversalBlockItemBehavior()
        {
            modId = "Universal",
            name = "block_item_behavior"
        });
    }

    // ---- I: items ----
    private void RegisterItems()
    {
        ResourceSystem.Instance.ItemDefinitions.Register(new ItemDefinition()
        {
            modId = ModId,
            name = "diamond_sword",
            LayerTextures = new string[]
            {
                "minecraft:diamond_sword"
            },
            MaxStack = 1,
            Tags = new() { "sword" }   // demo loot condition: zombie table bonus group
        });

        // Coal: fuel-tagged item, accepted by the furnace fuel slot filter.
        ResourceSystem.Instance.ItemDefinitions.Register(new ItemDefinition()
        {
            modId = ModId,
            name = "coal",
            LayerTextures = new string[] { $"{ModId}:coal" },
            MaxStack = 64,
            Tags = new() { "fuel" },
            CustomDatas = new()
            {
                ["minecraft:heat_value"] = CustomDataValueFactory.Of(1600)
            }
        });
    }
}
