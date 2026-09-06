using System.Collections.Generic;
public class ItemDefinition : ResourceType
{
    public string[] LayerTextures;
    public int MaxStack = 64;
    // Seconds of continuous right-hold required before the item's use settles
    // (eating/drinking). 0 = instant use on click (block items, ordinary items).
    public float UseTime = 0;
    public string ItemBehaivorId;
    public string BlockFullName;
    public bool IsBlockItem = false;
    // Semantic tags (e.g. "fuel"); inventory filtering and later logic (burn
    // values) query these.
    public List<string> Tags = null;

    public Dictionary<string, CustomDataValue> CustomDatas = null;
}