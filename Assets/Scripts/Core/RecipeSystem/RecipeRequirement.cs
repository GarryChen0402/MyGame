using System;

// One gate on a recipe beyond its item inputs: either a consumable cost (mod
// energy) or a pure condition (dimension, underwater...). Both share one
// mechanism so new gate semantics never add core fields: the document names a
// checker by full name, and the checker registry owns the implementation.
[Serializable]
public class RecipeRequirement
{
    public string Checker;   // checker full name ("mymod:mana_cost"); key into RequirementCheckers
    public string Param;     // checker-private parameter ("64" / "minecraft:nether"); may be empty
}

// Checker = registry object (same pattern as RecipeParserDefinition): its
// full name is what recipe documents may reference, and Freeze verifies every
// referenced checker is registered. The registry ships empty this phase - the
// mechanic skeleton is in place, no builtin checker registers yet.
public class RequirementCheckerDefinition : ResourceType
{
    public IRequirementChecker Checker;   // gate evaluation / cost consumption
}

// Two-phase gate protocol, aligned with vanilla "matches is read-only,
// consumption happens on take": CanSatisfy runs during matching/preview and
// must not mutate, Consume is called only when the take/complete path commits.
public interface IRequirementChecker
{
    // Read-only: can this gate currently pass? reason explains a rejection.
    bool CanSatisfy(RequirementContext ctx, string param, out string reason);
    // Commit phase: deduct the cost. Pure conditions leave this empty.
    void Consume(RequirementContext ctx, string param);
}

// Everything a checker may need: the hosting BlockEntity gives position,
// dimension and named DataContainer access (energy lives in a mod-defined
// container, checked and drained through it).
public class RequirementContext
{
    public BlockEntity Host;
}
