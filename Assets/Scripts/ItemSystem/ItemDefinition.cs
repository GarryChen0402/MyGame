public class ItemDefinition : ResourceType
{
    public string[] LayerTextures;
    public int MaxStack = 64;
    public string ItemBehaivorId;
    public string BlockFullName;
    public bool IsBlockItem = false;
    // Semantic tags (e.g. "fuel"); inventory filtering and later logic (burn
    // values) query these.
    public System.Collections.Generic.List<string> Tags = null;
}