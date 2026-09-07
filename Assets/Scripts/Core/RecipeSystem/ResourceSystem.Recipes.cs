using System;
using System.Collections.Generic;
using UnityEngine;

// Recipe registration pipeline, Freeze-time reference validation and the
// recipeType bucket index. Split from ResourceSystem.cs so the registry
// plumbing for the recipe triad stays in one file.
public partial class ResourceSystem
{
    // recipeType (parser full name) -> recipes in registration order; built
    // during Freeze, read-only afterwards via GetRecipesByRecipeType.
    private Dictionary<string, List<RecipeContent>> recipesByRecipeType;
    private static readonly List<RecipeContent> NoRecipes = new();

    // JSON shape of the requirements segment: camelCase keys, wrapped DTO
    // (JsonUtility cannot deserialize a bare top-level array).
    [Serializable]
    private class RequirementListDto { public List<RequirementEntryDto> list; }

    [Serializable]
    private class RequirementEntryDto
    {
        public string checker;
        public string param;
    }

    // ---- entry A: direct code registration (programmatic / debug) ----
    // Structure is the author's responsibility; RecipeType/Kind must line up
    // with a registered parser or Freeze validation aborts startup.
    public bool Register(RecipeContent content)
    {
        if (content == null)
        {
            Debug.LogError("[RecipeRegistry] direct registration rejected: null content");
            return false;
        }
        if (Recipes.ContainsValue(content.FullName))
        {
            Debug.LogError($"[RecipeRegistry] recipe '{content.FullName}' is already registered, duplicate rejected");
            return false;
        }
        return Recipes.Register(content);
    }

    // ---- entry B: text registration (the mod-tooling path) ----
    // json is the three-layer envelope (recipeTypeId/content/requirements).
    // Every failure logs and returns false without throwing, so one bad
    // document never interrupts the other registrations of the phase.
    public bool RegisterRecipeText(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            Debug.LogError("[RecipeRegistry] recipe document rejected: empty text");
            return false;
        }
        RecipeEnvelope envelope;
        try { envelope = JsonUtility.FromJson<RecipeEnvelope>(json); }
        catch (Exception e)
        {
            Debug.LogError($"[RecipeRegistry] recipe document rejected: envelope is malformed JSON ({e.Message})");
            return false;
        }
        if (envelope == null)
        {
            Debug.LogError("[RecipeRegistry] recipe document rejected: envelope is empty or 'null'");
            return false;
        }
        if (string.IsNullOrEmpty(envelope.recipeTypeId))
        {
            Debug.LogError("[RecipeRegistry] recipe document rejected: 'recipeTypeId' is missing or empty");
            return false;
        }
        if (string.IsNullOrEmpty(envelope.content))
        {
            Debug.LogError("[RecipeRegistry] recipe document rejected: 'content' is missing or empty");
            return false;
        }
        if (envelope.requirements == null)
        {
            Debug.LogError("[RecipeRegistry] recipe document rejected: 'requirements' is missing (use '[]' for none)");
            return false;
        }
        if (!RecipeParsers.TryGetResourceWithFullName(envelope.recipeTypeId, out var parser))
        {
            Debug.LogError($"[RecipeRegistry] recipe document rejected: recipeTypeId '{envelope.recipeTypeId}' is not registered (see RecipeParsers)");
            return false;
        }

        var result = parser.Parse(envelope.content);
        if (result == null || !result.Ok)
        {
            if (result?.Errors != null)
            {
                foreach (var err in result.Errors)
                    Debug.LogError($"[RecipeRegistry] {err}");
            }
            else
            {
                Debug.LogError($"[RecipeRegistry] recipe document rejected: parser '{envelope.recipeTypeId}' returned no result");
            }
            return false;
        }

        // Assemble the full content: type id from the envelope, requirements
        // from its third segment (common wrapper, never the parser's job).
        if (!TryParseRequirements(envelope.requirements, out var requirements))
            return false;
        result.Content.RecipeType = envelope.recipeTypeId;
        result.Content.Requirements = requirements;
        return Register(result.Content);   // same entry path as direct registration
    }

    private bool TryParseRequirements(string json, out List<RecipeRequirement> requirements)
    {
        requirements = null;
        string trimmed = json.Trim();
        if (!trimmed.StartsWith("[") || !trimmed.EndsWith("]"))
        {
            Debug.LogError("[RecipeRegistry] recipe document rejected: 'requirements' must be a JSON array text (e.g. '[]')");
            return false;
        }
        // JsonUtility cannot deserialize a bare top-level array, so the text
        // is wrapped in the fixed DTO.
        try
        {
            var dto = JsonUtility.FromJson<RequirementListDto>("{\"list\":" + trimmed + "}");
            var list = new List<RecipeRequirement>();
            if (dto?.list != null)
            {
                foreach (var e in dto.list)
                    list.Add(new RecipeRequirement { Checker = e?.checker, Param = e?.param });
            }
            requirements = list;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecipeRegistry] recipe document rejected: 'requirements' segment is malformed ({e.Message})");
            return false;
        }
    }

    // ---- runtime query: per-recipeType buckets (built at Freeze) ----

    // Recipes of one recipe type; empty list for unknown types and before the
    // index is built. WorkContainers scan only the buckets of their declared
    // supported types - no full-table sweeps.
    public IReadOnlyList<RecipeContent> GetRecipesByRecipeType(string recipeTypeFullName)
    {
        if (recipesByRecipeType == null || recipeTypeFullName == null) return NoRecipes;
        return recipesByRecipeType.TryGetValue(recipeTypeFullName, out var list) ? list : NoRecipes;
    }

    private void BuildRecipeTypeIndex()
    {
        recipesByRecipeType = new Dictionary<string, List<RecipeContent>>();
        foreach (var recipe in Recipes.Values)
        {
            if (!recipesByRecipeType.TryGetValue(recipe.RecipeType, out var list))
                recipesByRecipeType[recipe.RecipeType] = list = new List<RecipeContent>();
            list.Add(recipe);
        }
    }

    // ---- Freeze-time reference validation ----
    // Item ids, direct-entry RecipeType<->Kind consistency and requirement
    // checker references are only decidable now that every table is full.
    // Errors are collected in full, logged, then startup aborts.
    private void ValidateRecipeReferences()
    {
        var errors = new List<string>();
        foreach (var recipe in Recipes.Values)
        {
            string id = $"recipe '{recipe.FullName}'";
            if (string.IsNullOrEmpty(recipe.RecipeType))
                errors.Add($"{id}: RecipeType is empty (must be a registered parser full name)");
            else if (!RecipeParsers.TryGetResourceWithFullName(recipe.RecipeType, out var parser))
                errors.Add($"{id}: RecipeType '{recipe.RecipeType}' is not a registered parser");
            else if (parser.Kind != recipe.Kind)
                errors.Add($"{id}: RecipeType '{recipe.RecipeType}' kind mismatch ({recipe.Kind} != {parser.Kind})");

            CheckItemRefs(recipe.Inputs, "inputs", id, errors);
            CheckItemRefs(recipe.Outputs, "outputs", id, errors);
            if (recipe.ShapeKeys != null)
            {
                foreach (var kv in recipe.ShapeKeys)
                {
                    if (!ItemDefinitions.ContainsValue(kv.Value))
                        errors.Add($"{id}: unknown item reference '{kv.Value}' (shape key '{kv.Key}')");
                }
            }
            if (recipe.Requirements != null)
            {
                foreach (var req in recipe.Requirements)
                {
                    if (req == null) { errors.Add($"{id}: requirements contains a null entry"); continue; }
                    if (string.IsNullOrEmpty(req.Checker) || !RequirementCheckers.ContainsValue(req.Checker))
                        errors.Add($"{id}: unknown requirement checker '{req.Checker}'");
                }
            }
        }
        if (errors.Count == 0) return;
        foreach (var e in errors)
            Debug.LogError($"[RecipeRegistry] {e}");
        throw new InvalidOperationException(
            $"[RecipeRegistry] {errors.Count} recipe reference error(s) failed to resolve; startup aborted");
    }

    private void CheckItemRefs(List<ItemStackAmount> amounts, string what, string id, List<string> errors)
    {
        if (amounts == null) return;
        foreach (var a in amounts)
        {
            if (a != null && a.itemId != null && !ItemDefinitions.ContainsValue(a.itemId))
                errors.Add($"{id}: unknown item reference '{a.itemId}' ({what})");
        }
    }
}
