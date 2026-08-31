using UnityEngine;

// Processing module: the only module that ticks. Advances a matched recipe's
// progress, consumes inputs + fuel on completion and inserts the output.
// Input/Fuel/Output inventories are resolved by name against sibling
// InventoryModules; every access goes through their role-checked API with the
// Module requester, so an output inventory with InsertPolicy=Module accepts
// produced items while rejecting player insertion.
public class ProcessingModule : BlockEntityModule
{
    public InventoryModule Input, Fuel, Output;   // resolved in OnPlaced
    public float Progress;                        // 0..1 of the current recipe
    public RecipeDefinition CurrentRecipe;        // null when idle / no match

    private int lastInputVersion = -1;

    public ProcessingModule(ModuleDefinition config) { Config = config; }

    public override bool NeedsTick => true;

    public override void OnPlaced()
    {
        Input = GetModule<InventoryModule>(Config.InputInventory);
        Output = GetModule<InventoryModule>(Config.OutputInventory);
        if (Config.FuelInventory != null) Fuel = GetModule<InventoryModule>(Config.FuelInventory);
        if (Input == null) Debug.LogWarning($"[ProcessingModule] missing input inventory '{Config.InputInventory}'");
        if (Output == null) Debug.LogWarning($"[ProcessingModule] missing output inventory '{Config.OutputInventory}'");
    }

    public override void Tick(float deltaTime)
    {
        if (Input == null || Output == null || Host.Removed) return;
        if (Fuel != null && !HasFuel())
        {
            // Out of fuel: pause and forget the in-flight recipe.
            CurrentRecipe = null;
            Progress = 0f;
            return;
        }

        // Re-match when the input content changed or no recipe is cached.
        if (CurrentRecipe == null || Input.Version != lastInputVersion)
        {
            lastInputVersion = Input.Version;
            CurrentRecipe = FindRecipe();
            if (CurrentRecipe == null) { Progress = 0f; return; }
        }

        Progress += deltaTime / CurrentRecipe.ProcessingTime;
        if (Progress < 1f) return;

        // Complete: room for the output is pre-checked so consume never has to
        // roll back; if the output is full, hold progress at 1 and retry.
        if (!CanFitOutput(CurrentRecipe)) { Progress = 1f; return; }
        if (!ConsumeInputs(CurrentRecipe)) { CurrentRecipe = null; Progress = 0f; return; }
        if (Fuel != null) Fuel.TryExtract(0, 1, InventoryAccess.Module);
        InsertOutput(CurrentRecipe);
        Progress = 0f;
        CurrentRecipe = null;   // input version changed via the extracts above
    }

    private bool HasFuel()
    {
        foreach (var stack in Fuel.Inventory.itemStacks)
            if (stack != null && !stack.IsEmpty()) return true;
        return false;
    }

    private RecipeDefinition FindRecipe()
    {
        if (string.IsNullOrEmpty(Config.RecipeType)) return null;
        foreach (var recipe in ResourceSystem.Instance.Recipes.Values)
        {
            if (recipe.RecipeTypeFullName != Config.RecipeType) continue;
            if (HasAllInputs(recipe) && CanFitOutput(recipe)) return recipe;
        }
        return null;
    }

    private bool HasAllInputs(RecipeDefinition recipe)
    {
        if (recipe.Inputs == null) return false;
        foreach (var req in recipe.Inputs)
        {
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(req.itemId, out ushort id)) return false;
            int have = 0;
            foreach (var stack in Input.Inventory.itemStacks)
                if (stack != null && !stack.IsEmpty() && stack.itemId == id) have += stack.amount;
            if (have < req.amount) return false;
        }
        return true;
    }

    private bool CanFitOutput(RecipeDefinition recipe)
    {
        if (recipe.Outputs == null) return false;
        foreach (var o in recipe.Outputs)
        {
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(o.itemId, out ushort id)) return false;
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(id, out var def)) return false;
            int room = 0;
            foreach (var stack in Output.Inventory.itemStacks)
                if (stack != null && !stack.IsEmpty() && stack.itemId == id) room += def.MaxStack - stack.amount;
            int emptySlots = Output.Inventory.MaxSlotCount - Output.Inventory.itemStacks.Count;
            room += emptySlots * def.MaxStack;
            if (room < o.amount) return false;
        }
        return true;
    }

    private bool ConsumeInputs(RecipeDefinition recipe)
    {
        foreach (var req in recipe.Inputs)
        {
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(req.itemId, out ushort id)) return false;
            int remaining = req.amount;
            for (int i = 0; i < Input.Inventory.itemStacks.Count && remaining > 0; i++)
            {
                var stack = Input.Inventory.itemStacks[i];
                if (stack == null || stack.IsEmpty() || stack.itemId != id) continue;
                int take = Mathf.Min(remaining, stack.amount);
                if (!Input.TryExtract(i, take, InventoryAccess.Module)) return false;
                remaining -= take;
            }
            if (remaining > 0) return false;
        }
        return true;
    }

    private void InsertOutput(RecipeDefinition recipe)
    {
        foreach (var o in recipe.Outputs)
        {
            if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(o.itemId, out ushort id)) continue;
            Output.TryInsert(new ItemStack { itemId = id, amount = o.amount }, InventoryAccess.Module);
        }
    }

    // ---- serialization: progress only; the recipe re-matches from input on load ----

    [System.Serializable]
    private class ProcessingSaveData
    {
        public float progress;
    }

    public override string SerializeToJson()
        => JsonUtility.ToJson(new ProcessingSaveData { progress = Progress });

    public override void DeserializeFromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<ProcessingSaveData>(json);
        Progress = data.progress;
        lastInputVersion = -1;   // force a re-match on the first tick
    }
}
