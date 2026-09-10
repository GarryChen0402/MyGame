public partial class Minecraft
{
    // ---- J: recipe parsers ----
    private void RegisterRecipeParsers()
    {
        // ---- recipe parsers: their full name is the recipe type id ----
        ResourceSystem.Instance.RecipeParsers.Register(BuiltinRecipeParsers.Shaped());
        ResourceSystem.Instance.RecipeParsers.Register(BuiltinRecipeParsers.Shapeless());
        ResourceSystem.Instance.RecipeParsers.Register(BuiltinRecipeParsers.Processing());
    }

    // ---- K: recipe texts ----
    private void RegisterRecipeTexts()
    {
        // ---- demo recipes via the text entry (three-layer envelopes; the
        // content text below is the inner layer, Build escapes it into the
        // envelope document) ----

        // Furnace: 1 cobblestone -> 1 stone (short duration for validation).
        ResourceSystem.Instance.RegisterRecipeText(RecipeEnvelope.Build(
            "universal:processing",
            @"{ ""modId"": ""minecraft"", ""name"": ""smelt_cobblestone"", ""inputs"": [ { ""itemId"": ""minecraft:cobblestone"", ""amount"": 1 } ], ""outputs"": [ { ""itemId"": ""minecraft:stone"", ""amount"": 1 } ], ""durationTicks"": 20 }",
            "[]"));

        // Workbench shaped: 8 cobblestone in a ring -> 1 furnace.
        ResourceSystem.Instance.RegisterRecipeText(RecipeEnvelope.Build(
            "universal:shaped",
            @"{ ""modId"": ""minecraft"", ""name"": ""crafting_furnace"", ""pattern"": [""CCC"", ""C C"", ""CCC""], ""keys"": [ { ""symbol"": ""C"", ""item"": ""minecraft:cobblestone"" } ], ""outputs"": [ { ""itemId"": ""minecraft:furnace"", ""amount"": 1 } ] }",
            "[]"));

        // Workbench shaped: 3 stone in a column -> 1 stone stair.
        ResourceSystem.Instance.RegisterRecipeText(RecipeEnvelope.Build(
            "universal:shaped",
            @"{ ""modId"": ""minecraft"", ""name"": ""crafting_stone_stair"", ""pattern"": [""S"", ""S"", ""S""], ""keys"": [ { ""symbol"": ""S"", ""item"": ""minecraft:stone"" } ], ""outputs"": [ { ""itemId"": ""minecraft:stone_stair"", ""amount"": 1 } ] }",
            "[]"));

        // Workbench shapeless: 4 dirt -> 1 grass.
        ResourceSystem.Instance.RegisterRecipeText(RecipeEnvelope.Build(
            "universal:shapeless",
            @"{ ""modId"": ""minecraft"", ""name"": ""crafting_grass"", ""inputs"": [ { ""itemId"": ""minecraft:dirt"", ""amount"": 4 } ], ""outputs"": [ { ""itemId"": ""minecraft:grass"", ""amount"": 1 } ] }",
            "[]"));
    }
}
