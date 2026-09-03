using System;
using System.Collections.Generic;
using UnityEngine;

public class ProcessingWorkContainer : WorkContainer
{
    [Serializable]
    public class Config
    {
        public string Input, Fuel, Output;
        public float SpeedMultiplier = 1.0f;
    }

    private readonly Config config;
    // Recipe types (parser full names) this container can run, e.g.
    // {"universal:processing"} (WorkContainerConfig.SupportedRecipeTypes).
    private readonly List<string> supportedRecipeTypes;

    // Temporary flat fuel value until ItemDefinition gains a real burn-time field.
    public const int FUEL_BURN_TICKS = 200;

    public InventoryDataContainer Input, Output, Fuel;
    public int CurrentTickProgress;   // ticks burned so far (vanilla cookingProgress)
    public int TotalTickTime;         // target ticks for the current recipe
    public int FuelLeftTickTime;      // remaining fuel ticks (vanilla litTime)
    public RecipeContent CurrentRecipe;

    public ProcessingWorkContainer(WorkContainerConfig config)
    {
        this.config = JsonUtility.FromJson<Config>(config.Parameters);
        supportedRecipeTypes = config.SupportedRecipeTypes;
    }

    public override void OnBind(BlockEntity blockEntity)
    {
        base.OnBind(blockEntity);
        if (config == null) return;
        Input = GetData<InventoryDataContainer>(config.Input);
        Fuel = GetData<InventoryDataContainer>(config.Fuel);
        Output = GetData<InventoryDataContainer>(config.Output);
        if (Input == null || Fuel == null || Output == null)
            Debug.LogWarning("[ProcessingWorkContainer] missing input/fuel/output container(s), disabled");
    }

    // Vanilla-style integer tick: one Tick() call = one tick.
    public override void Tick()
    {
        if(Input == null || Output == null || Fuel == null)return;

        // Re-match when nothing burns or the current recipe no longer fits the
        // input; vanilla resets progress when no recipe can run. Progress runs
        // only while canRun, but the lit fire burns down regardless (below).
        if (CurrentRecipe == null || !MatchesInput(CurrentRecipe))
        {
            CurrentRecipe = FindRecipe();
            CurrentTickProgress = 0;
            if (CurrentRecipe != null) TotalTickTime = ToTicks(CurrentRecipe);
        }

        // Output blocked -> vanilla resets progress until it fits again.
        bool canRun = CurrentRecipe != null;
        if (canRun && !CanFitOutput(CurrentRecipe))
        {
            CurrentTickProgress = 0;
            canRun = false;
        }

        // A lit fire always burns down one tick whether or not a recipe can
        // run (vanilla wastes the remaining burn once the input runs out); a
        // new fuel piece is lit only when a recipe can actually run.
        bool changed = false;
        if (FuelLeftTickTime > 0)
        {
            FuelLeftTickTime--;
            changed = true;
            if (canRun)
            {
                CurrentTickProgress++;
                if (CurrentTickProgress >= TotalTickTime)
                {
                    if (TryCraft(CurrentRecipe))
                    {
                        CurrentTickProgress = 0;
                        CurrentRecipe = FindRecipe();   // next recipe under the remaining input
                        if (CurrentRecipe != null) TotalTickTime = ToTicks(CurrentRecipe);
                    }
                }
            }
        }
        else if (canRun && TryConsumeFuel())
        {
            FuelLeftTickTime = FUEL_BURN_TICKS;   // lit fire starts next tick
            changed = true;
        }
        if (changed) MarkDirty();
    }

    private int ToTicks(RecipeContent recipe)
        => Mathf.Max(1, Mathf.RoundToInt(recipe.ProcessingTickTime / config.SpeedMultiplier));

    private RecipeContent FindRecipe()
    {
        if (supportedRecipeTypes == null) return null;
        foreach (var recipeType in supportedRecipeTypes)
        {
            foreach (var recipe in ResourceSystem.Instance.GetRecipesByRecipeType(recipeType))
            {
                // Only Processing recipes run here; a mismatched direct
                // registration is already rejected at Freeze, skip defensively.
                if (recipe.Kind != RecipeKind.Processing) continue;
                if (MatchesInput(recipe)) return recipe;
            }
        }
        return null;
    }

    private bool MatchesInput(RecipeContent recipe)
    {
        foreach (var need in recipe.Inputs)
        {
            if (!TryGetAvailable(need.itemId, out int have) || have < need.amount) return false;
        }
        return true;
    }

    // Checks every output fits (policy/whitelist + merge capacity) without mutating.
    private bool CanFitOutput(RecipeContent recipe)
    {
        foreach (var outEntry in recipe.Outputs)
        {
            if (!Output.CanInsert(new ItemStack { itemId = GetItemId(outEntry.itemId), amount = outEntry.amount },
                    ContainerAccess.Module))
                return false;
        }
        return true;
    }

    private bool TryCraft(RecipeContent recipe)
    {
        foreach (var need in recipe.Inputs)
        {
            if (!ConsumeInput(need)) return false;
        }
        foreach (var outEntry in recipe.Outputs)
        {
            var stack = new ItemStack { itemId = GetItemId(outEntry.itemId), amount = outEntry.amount };
            if (!Output.TryInsert(stack, ContainerAccess.Module))
                Debug.LogError("[ProcessingWorkContainer] output insert failed after capacity check");
        }
        return true;
    }

    private bool TryConsumeFuel()
    {
        for (int i = 0; i < Fuel.Inv.itemStacks.Count; i++)
        {
            var slot = Fuel.Inv.GetItemStackAt(i);
            if (slot == null || slot.IsEmpty()) continue;
            Fuel.Inv.TryConsumeItemAt(i, 1);
            return true;
        }
        return false;
    }

    private bool ConsumeInput(ItemStackAmount need)
    {
        if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(need.itemId, out ushort id)) return false;
        int left = need.amount;
        for (int i = 0; i < Input.Inv.itemStacks.Count && left > 0; i++)
        {
            var slot = Input.Inv.GetItemStackAt(i);
            if (slot == null || slot.IsEmpty() || slot.itemId != id) continue;
            int take = Mathf.Min(left, slot.amount);
            if (!Input.Inv.TryConsumeItemAt(i, take)) return false;
            left -= take;
        }
        return left == 0;
    }

    private bool TryGetAvailable(string fullName, out int total)
    {
        total = 0;
        if (!ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(fullName, out ushort id)) return false;
        foreach (var slot in Input.Inv.itemStacks)
            if (slot.itemId == id) total += slot.amount;
        return true;
    }

    private ushort GetItemId(string fullName)
        => ResourceSystem.Instance.ItemDefinitions.TryGetNumberId(fullName, out ushort id) ? id : (ushort)0;

    // ---- persistence ----

    // Runtime tick state only (design doc §7): the recipe reference is never
    // persisted - restore re-matches it from the input and keeps the saved
    // progress only when the same recipe still fits.
    [System.Serializable]
    public class SaveData
    {
        public int progress;
        public int totalTicks;
        public int fuelLeft;
    }

    public override string Serialize() => JsonUtility.ToJson(new SaveData
    {
        progress = CurrentTickProgress,
        totalTicks = TotalTickTime,
        fuelLeft = FuelLeftTickTime
    });

    public override void Deserialize(string json)
    {
        var save = JsonUtility.FromJson<SaveData>(json);
        if (save == null) return;
        CurrentTickProgress = save.progress;
        TotalTickTime = save.totalTicks;
        FuelLeftTickTime = save.fuelLeft;
        // Re-match now so Tick resumes instead of restarting: a matching recipe
        // with the same total ticks keeps the progress; anything else (input
        // changed, recipe gone) falls back to a fresh start.
        CurrentRecipe = FindRecipe();
        if (CurrentRecipe == null || ToTicks(CurrentRecipe) != TotalTickTime)
        {
            CurrentTickProgress = 0;
            TotalTickTime = 0;
        }
    }
}