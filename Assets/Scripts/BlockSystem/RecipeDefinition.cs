using System.Collections.Generic;

// One recipe input/output entry: item full name + amount.
public class ItemStackAmount
{
    public string itemId;   // item full name ("minecraft:cobblestone")
    public int amount;
}

// A concrete recipe - ResourceType. Crafting recipes optionally declare a
// Shape (row strings, symbols resolved via ShapeKeys); null Shape = loose
// multiset matching. Processing recipes use Inputs/Outputs/ProcessingTickTime.
public class RecipeDefinition : ResourceType
{
    public string RecipeTypeFullName;              // owning RecipeType full name
    public List<ItemStackAmount> Inputs = null;    // processing/loose-crafting inputs
    public List<ItemStackAmount> Outputs = null;   // outputs
    public int ProcessingTickTime = 20;              // processing duration in ticks
    public string[] Shape = null;                  // crafting shape, e.g. {"AAA","ABA","AAA"}
    public Dictionary<char, string> ShapeKeys = null;   // shape symbol -> item full name
}
