using System;
using System.Collections.Generic;
using UnityEngine;

// Result of parsing one recipe document's content segment. Content carries the
// parser-produced definition; RecipeType and Requirements are assembled later
// by the text entry, so a parser never sees the envelope's other two layers.
public class RecipeParseResult
{
    public bool Ok;
    public List<string> Errors;          // populated when Ok == false
    public RecipeContent Content;        // populated when Ok == true
    public List<RecipeRequirement> Requirements;   // envelope.requirements, deserialized by the text entry
}

// Parser = registry object (same pattern as DataContainerDefinition /
// WorkContainerDefinition). Its registered full name IS the recipe type id:
// documents locate it, contents carry it back (RecipeContent.RecipeType) and
// Freeze buckets recipes by it. New recipe forms = one new registered entry,
// no core changes.
public class RecipeParserDefinition : ResourceType
{
    public RecipeKind Kind;                          // form this parser emits (copied into Content.Kind)
    public Func<string, RecipeParseResult> Parse;    // content segment string -> parse result
}

// Outer three-layer envelope of a recipe document. The text entry deserializes
// these three strings first, then handles each layer independently.
[Serializable]
public class RecipeEnvelope
{
    public string recipeTypeId;      // recipe type = key into RecipeParsers
    public string content;           // inner JSON text, parsed by the located parser
    public string requirements;      // inner JSON array text ({checker,param}[]), common deserialization

    // Wraps already-written inner JSON texts into a full envelope document;
    // the JSON writer escapes their embedded quotes. Mods shipping text
    // resources skip this - they hand the file content to RegisterRecipeText
    // directly.
    public static string Build(string recipeTypeId, string content, string requirements)
        => JsonUtility.ToJson(new RecipeEnvelope
        {
            recipeTypeId = recipeTypeId,
            content = content,
            requirements = requirements
        });
}

// ---- content-segment DTOs (one per builtin parser; JsonUtility shapes) ----

[Serializable]
public class SymbolItemDto
{
    public string symbol;    // single character of the pattern row
    public string item;      // item full name
}

[Serializable]
public class ShapedContentDto
{
    public string modId;
    public string name;
    public string[] pattern;                     // shape rows; ' ' = empty cell
    public List<SymbolItemDto> keys;             // symbol table (array form: no Dictionary in JsonUtility)
    public List<ItemStackAmount> outputs;
}

[Serializable]
public class ShapelessContentDto
{
    public string modId;
    public string name;
    public List<ItemStackAmount> inputs;
    public List<ItemStackAmount> outputs;
}

[Serializable]
public class ProcessingContentDto
{
    public string modId;
    public string name;
    public List<ItemStackAmount> inputs;
    public List<ItemStackAmount> outputs;
    public int durationTicks = 20;   // default when the document omits it
}

// The three builtin parsers, registered as universal:* recipe types.
public static class BuiltinRecipeParsers
{
    public static RecipeParserDefinition Shaped() => new()
    {
        modId = "universal",
        name = "shaped",
        Kind = RecipeKind.Shaped,
        Parse = ParseShaped
    };

    public static RecipeParserDefinition Shapeless() => new()
    {
        modId = "universal",
        name = "shapeless",
        Kind = RecipeKind.Shapeless,
        Parse = ParseShapeless
    };

    public static RecipeParserDefinition Processing() => new()
    {
        modId = "universal",
        name = "processing",
        Kind = RecipeKind.Processing,
        Parse = ParseProcessing
    };

    // ---- shared structural checks ----

    // Validates the modId/name header; returns the id prefix used by later
    // error lines ("recipe 'foo:bar'" or "recipe document" when unknown).
    private static string CheckHeader(string modId, string name, List<string> errors)
    {
        bool hasMod = !string.IsNullOrEmpty(modId);
        bool hasName = !string.IsNullOrEmpty(name);
        if (!hasMod) errors.Add("recipe document: 'modId' is missing or empty");
        if (!hasName) errors.Add("recipe document: 'name' is missing or empty");
        return hasMod && hasName ? $"recipe '{modId}:{name}'" : "recipe document";
    }

    // Every entry needs an item id and a positive amount; the list itself
    // must be non-empty (a recipe without inputs/outputs is meaningless).
    private static void CheckAmounts(List<ItemStackAmount> amounts, string what, string id, List<string> errors)
    {
        if (amounts == null || amounts.Count == 0)
        {
            errors.Add($"{id}: '{what}' must be non-empty");
            return;
        }
        for (int i = 0; i < amounts.Count; i++)
        {
            var a = amounts[i];
            if (a == null || string.IsNullOrEmpty(a.itemId)) errors.Add($"{id}: '{what}[{i}]' has no itemId");
            else if (a.amount <= 0) errors.Add($"{id}: '{what}[{i}]' ({a.itemId}) amount must be > 0");
        }
    }

    private static RecipeParseResult Fail(List<string> errors)
        => new() { Ok = false, Errors = errors };

    private static RecipeParseResult Ok(RecipeContent content)
        => new() { Ok = true, Content = content };

    // ---- universal:shaped ----

    private static RecipeParseResult ParseShaped(string content)
    {
        var errors = new List<string>();
        var dto = FromJson<ShapedContentDto>(content, errors);
        if (dto == null) return Fail(errors);
        string id = CheckHeader(dto.modId, dto.name, errors);

        bool patternOk = false;
        if (dto.pattern == null || dto.pattern.Length == 0)
            errors.Add($"{id}: 'pattern' must be non-empty");
        else
        {
            patternOk = true;
            int rowLength = -1;
            foreach (var row in dto.pattern)
            {
                if (row == null)
                {
                    errors.Add($"{id}: pattern contains a null row");
                    patternOk = false;
                    break;
                }
                if (rowLength < 0) rowLength = row.Length;
                else if (row.Length != rowLength)
                {
                    errors.Add($"{id}: pattern rows have unequal lengths");
                    patternOk = false;
                    break;
                }
            }
        }

        var shapeKeys = new Dictionary<char, string>();
        bool keysOk = true;
        if (dto.keys == null || dto.keys.Count == 0)
        {
            errors.Add($"{id}: 'keys' must be non-empty");
            keysOk = false;
        }
        else
        {
            foreach (var k in dto.keys)
            {
                if (k == null) { errors.Add($"{id}: 'keys' contains a null entry"); keysOk = false; continue; }
                if (string.IsNullOrEmpty(k.item)) { errors.Add($"{id}: key '{k.symbol}' has no item"); keysOk = false; continue; }
                if (k.symbol == null || k.symbol.Length != 1)
                {
                    errors.Add($"{id}: key symbol '{k.symbol}' must be a single character");
                    keysOk = false;
                    continue;
                }
                char symbol = k.symbol[0];
                if (symbol == ' ')
                {
                    errors.Add($"{id}: ' ' (space) cannot be used as a shape key symbol");
                    keysOk = false;
                    continue;
                }
                if (shapeKeys.ContainsKey(symbol))
                {
                    errors.Add($"{id}: key symbol '{symbol}' is duplicated");
                    keysOk = false;
                    continue;
                }
                shapeKeys[symbol] = k.item;
            }
        }

        if (patternOk && keysOk)
        {
            foreach (var row in dto.pattern)
            {
                foreach (char c in row)
                {
                    if (c != ' ' && !shapeKeys.ContainsKey(c))
                        errors.Add($"{id}: pattern symbol '{c}' has no matching 'keys' entry");
                }
            }
        }

        CheckAmounts(dto.outputs, "outputs", id, errors);
        if (errors.Count > 0) return Fail(errors);

        return Ok(new RecipeContent
        {
            modId = dto.modId,
            name = dto.name,
            Kind = RecipeKind.Shaped,
            Shape = dto.pattern,
            ShapeKeys = shapeKeys,
            Outputs = dto.outputs
        });
    }

    // ---- universal:shapeless / universal:processing ----

    private static RecipeParseResult ParseShapeless(string content)
    {
        var errors = new List<string>();
        var dto = FromJson<ShapelessContentDto>(content, errors);
        if (dto == null) return Fail(errors);
        string id = CheckHeader(dto.modId, dto.name, errors);
        CheckAmounts(dto.inputs, "inputs", id, errors);
        CheckAmounts(dto.outputs, "outputs", id, errors);
        if (errors.Count > 0) return Fail(errors);

        return Ok(new RecipeContent
        {
            modId = dto.modId,
            name = dto.name,
            Kind = RecipeKind.Shapeless,
            Inputs = dto.inputs,
            Outputs = dto.outputs
        });
    }

    private static RecipeParseResult ParseProcessing(string content)
    {
        var errors = new List<string>();
        var dto = FromJson<ProcessingContentDto>(content, errors);
        if (dto == null) return Fail(errors);
        string id = CheckHeader(dto.modId, dto.name, errors);
        CheckAmounts(dto.inputs, "inputs", id, errors);
        CheckAmounts(dto.outputs, "outputs", id, errors);
        if (dto.durationTicks <= 0)
            errors.Add($"{id}: 'durationTicks' must be > 0");
        if (errors.Count > 0) return Fail(errors);

        return Ok(new RecipeContent
        {
            modId = dto.modId,
            name = dto.name,
            Kind = RecipeKind.Processing,
            Inputs = dto.inputs,
            Outputs = dto.outputs,
            ProcessingTickTime = dto.durationTicks
        });
    }

    private static T FromJson<T>(string content, List<string> errors)
    {
        try
        {
            var dto = JsonUtility.FromJson<T>(content);
            if (dto == null) errors.Add("recipe document: content segment is empty or 'null'");
            return dto;
        }
        catch (Exception e)
        {
            errors.Add($"recipe document: content segment is malformed JSON ({e.Message})");
            return default;
        }
    }
}
