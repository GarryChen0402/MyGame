using System.Collections.Generic;

// One recipe input/output entry: item full name + amount.
public class ItemStackAmount
{
    public string itemId;   // item full name ("minecraft:cobblestone")
    public int amount;
}

// A concrete recipe - ResourceType. Crafting recipes optionally declare a
// Shape (row strings, symbols resolved via ShapeKeys); null Shape = loose
// multiset matching. Processing recipes use Inputs/Outputs/ProcessingTime.
public class RecipeDefinition : ResourceType
{
    public string RecipeTypeFullName;              // owning RecipeType full name
    public List<ItemStackAmount> Inputs = null;    // processing/loose-crafting inputs
    public List<ItemStackAmount> Outputs = null;   // outputs
    public float ProcessingTime = 3f;              // processing duration in seconds
    public string[] Shape = null;                  // crafting shape, e.g. {"AAA","ABA","AAA"}
    public Dictionary<char, string> ShapeKeys = null;   // shape symbol -> item full name
}
