using System.Collections.Generic;

public class LootContext
{
    public Entity Operator;     // breaker / killer; null when no actor (rule §2.1)
    public ItemStack heldItem;  // operator's held stack; may be null/empty
}

public interface ICondition
{
    bool Pass(LootContext ctx); // conditions decide null-Operator semantics themselves
}

public enum LootLogic { And, Or }

// Composite pattern: a group IS a condition, so groups nest into arbitrary
// decision trees (rule §2.1).
public class ConditionGroup : ICondition
{
    private readonly List<ICondition> children;
    private readonly LootLogic logic;

    private ConditionGroup(LootLogic logic, List<ICondition> children)
    {
        this.logic = logic;
        this.children = children;
    }

    public static ConditionGroup And(params ICondition[] cs) => new(LootLogic.And, new List<ICondition>(cs));
    public static ConditionGroup Or(params ICondition[] cs) => new(LootLogic.Or, new List<ICondition>(cs));

    public bool Pass(LootContext ctx)
    {
        if(logic == LootLogic.And)
        {
            foreach(var c in children)if(!c.Pass(ctx))return false;
            return true;
        }
        foreach(var c in children)if(c.Pass(ctx))return true;
        return false;
    }
}

// Example leaf conditions (mods write their own ICondition classes freely -
// table definitions are code, no condition registry in phase 1).
public sealed class OperatorIsPlayerCondition : ICondition
{
    public bool Pass(LootContext ctx) => ctx.Operator is Player;
}

// Passes when the operator holds a stack whose item definition carries the
// tag (example second condition, rule §2.1). Null/empty held item fails.
public sealed class OperatorHoldsTagCondition : ICondition
{
    private readonly string tag;

    public OperatorHoldsTagCondition(string tag) => this.tag = tag;

    public bool Pass(LootContext ctx)
    {
        ItemStack held = ctx.heldItem;
        if(held == null || held.IsEmpty())return false;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(held.itemId, out var def))return false;
        return def.Tags != null && def.Tags.Contains(tag);
    }
}
