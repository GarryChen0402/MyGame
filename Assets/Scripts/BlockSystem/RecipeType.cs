// Recipe category (registry object), e.g. "minecraft:crafting" / "minecraft:furance".
// A RecipeDefinition belongs to exactly one RecipeType; modules declare which
// type they process. New categories = new registry entries, no code changes.
public class RecipeType : ResourceType { }
