using System;
using System.Collections.Generic;

// One recipe input/output entry: item full name + amount (the serialized
// shape shared by content documents and direct-registered recipes).
[Serializable]
public class ItemStackAmount
{
    public string itemId;   // item full name ("minecraft:cobblestone")
    public int amount;
}

// Form family a parser produces. Consumers dispatch on Kind instead of
// inferring shape from null fields, and Freeze cross-checks Kind against the
// owning parser so mismatched documents fail loudly.
public enum RecipeKind { Shaped, Shapeless, Processing }

// A concrete recipe - ResourceType, the "content" leg of the recipe triad.
// RecipeType is the owning parser's full name (e.g. "universal:shaped"): the
// key used to look a content up in RecipeParsers, bucket recipes at Freeze,
// and let containers query what they can run.
public class RecipeContent : ResourceType
{
    public string RecipeType;                       // parser full name ("universal:shaped"); Recipes key into RecipeParsers
    public RecipeKind Kind;                         // form; text entry takes it from the parser, direct authors fill it
    public List<ItemStackAmount> Inputs = null;     // processing / shapeless inputs
    public List<ItemStackAmount> Outputs = null;    // outputs
    public int ProcessingTickTime = 20;             // processing duration in ticks
    public string[] Shape = null;                   // crafting shape rows, e.g. {"CCC","C C","CCC"}; shaped only
    public Dictionary<char, string> ShapeKeys = null;   // shape symbol -> item full name; shaped only
    public List<RecipeRequirement> Requirements = null; // cost/condition gates (RequirementCheckers); optional
}
