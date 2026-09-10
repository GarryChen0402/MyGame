using System;
using System.Collections.Generic;
using UnityEngine;

public class ProcessingWorkContainer : WorkContainer, IChannelSource
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

    // Burn time now lives on the fuel item: ItemDefinition.CustomDatas under
    // the key below (coal registers 1600 ticks, vanilla's 80 s). A "fuel"-tagged
    // item without the value is treated as unburnable.
    private const string HeatValueKey = "minecraft:heat_value";
    private string rejectedFuelFullName;   // last fuel rejected for a missing burn value (warn once)

    public InventoryDataContainer Input, Output, Fuel;
    public int CurrentTickProgress;   // ticks burned so far (vanilla cookingProgress)
    public int TotalTickTime;         // target ticks for the current recipe
    public int FuelLeftTickTime;      // remaining fuel ticks (vanilla litTime)
    public int CurrentFuelTotalTicks; // total ticks of the lit fuel piece (vanilla burnTime, fire gauge denominator)
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
        else if (canRun && TryConsumeFuel(out int burnTicks))
        {
            FuelLeftTickTime = burnTicks;             // lit fire starts next tick
            CurrentFuelTotalTicks = burnTicks;
            changed = true;
        }
        if (changed) MarkDirty();
    }

    private int ToTicks(RecipeContent recipe)
        => Mathf.Max(1, Mathf.RoundToInt(recipe.ProcessingTickTime / config.SpeedMultiplier));

    // ---- panel channels ----

    // The four tick counters as generic integer channels (the former
    // FurnaceProgressView, now one segment of the panel's PanelData packet).
    // Order mirrors vanilla's AbstractFurnaceMenu data slots.
    public int ChannelCount => 4;

    public void ReadChannels(PanelData data, int start)
    {
        data.ApplyChannel(start, FuelLeftTickTime);          // litTime
        data.ApplyChannel(start + 1, CurrentFuelTotalTicks); // burnTime
        data.ApplyChannel(start + 2, CurrentTickProgress);   // cookingProgress
        data.ApplyChannel(start + 3, TotalTickTime);         // cookingTotalTime
    }

    // ---- panel self-report ----

    // Furnace panel: one slot per data container (input/fuel/output) and the
    // four tick counters as channels. Names follow vanilla's furnace menu
    // roles; channel order mirrors the ReadChannels order above.
    public override void DescribePanel(PanelBuildContext ctx)
    {
        if(Input == null || Fuel == null || Output == null)return;   // unbound container: contributes nothing
        ctx.AddSlot("input", Input.Inv, 0, new ContainerSlotAccess(Input, 0));
        ctx.AddSlot("fuel", Fuel.Inv, 0, new ContainerSlotAccess(Fuel, 0));
        ctx.AddSlot("output", Output.Inv, 0, new ContainerSlotAccess(Output, 0));
        ctx.AddChannels(new[] { "fuel_left", "fuel_total", "cook_progress", "cook_total" }, this);
    }

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

    private bool TryConsumeFuel(out int burnTicks)
    {
        burnTicks = 0;
        for (int i = 0; i < Fuel.Inv.itemStacks.Count; i++)
        {
            var slot = Fuel.Inv.GetItemStackAt(i);
            if (slot == null || slot.IsEmpty()) continue;
            if (slot.itemId == 0
                || !ResourceSystem.Instance.ItemDefinitions
                    .TryGetResourceWithNumberId(slot.itemId, out var def)
                || !TryGetBurnTicks(def, out burnTicks))
                continue;
            Fuel.Inv.TryConsumeItemAt(i, 1);
            return true;
        }
        return false;
    }

    // Burn time is read from the item definition's CustomDatas (coal registers
    // 1600 ticks). A missing/zero value means the item never lights; warn once
    // per item kind instead of spamming every tick it sits in the fuel slot.
    private bool TryGetBurnTicks(ItemDefinition def, out int burnTicks)
    {
        burnTicks = 0;
        if (def.CustomDatas != null
            && def.CustomDatas.TryGetValue(HeatValueKey, out var raw)
            && raw is CustomDataValue<int> typed && typed.Value > 0)
        {
            burnTicks = typed.Value;
            return true;
        }
        if (rejectedFuelFullName != def.FullName)
        {
            rejectedFuelFullName = def.FullName;
            Debug.LogWarning($"[ProcessingWorkContainer] {def.FullName} is tagged as fuel but has no valid {HeatValueKey}, won't burn");
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
        // The lit piece's total ticks are not persisted; approximate from the
        // saved remainder so the fire gauge decays instead of dividing by zero.
        CurrentFuelTotalTicks = save.fuelLeft;
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