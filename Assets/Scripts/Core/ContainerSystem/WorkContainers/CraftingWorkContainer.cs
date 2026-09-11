using System;
using System.Collections.Generic;
using UnityEngine;

// Thin adapter wiring a BlockEntity's grid/result data containers into a
// CraftingSolver (design doc Docs/玩家界面-模型预览组件与2x2个人合成设计方案.md
// §5.1.2). All matching/preview/consume logic lives in the solver; this class
// only resolves the container names declared in its config, forwards the
// ICraftingGridHost callbacks and keeps the work-container persistence
// contract (grid data persists via its own container, preview never saved).
public class CraftingWorkContainer : WorkContainer, ICraftingGridHost
{
    [Serializable]
    public class Config
    {
        public string Grid, Result;
        public int GridWidth = 3, GridHeight = 3;
    }

    private readonly Config config;
    // Recipe types (parser full names) this container can run, e.g.
    // {"universal:shaped", "universal:shapeless"} (WorkContainerConfig.SupportedRecipeTypes).
    private readonly List<string> supportedRecipeTypes;

    private CraftingSolver solver;

    public CraftingWorkContainer(WorkContainerConfig config)
    {
        this.config = JsonUtility.FromJson<Config>(config.Parameters);
        supportedRecipeTypes = config.SupportedRecipeTypes;
    }

    public override void OnBind(BlockEntity blockEntity)
    {
        base.OnBind(blockEntity);
        if(config == null)return;
        var grid = GetData<InventoryDataContainer>(config.Grid);
        var result = GetData<InventoryDataContainer>(config.Result);
        if(grid == null || result == null)
        {
            Debug.LogWarning("[CraftingWorkContainer] missing grid/result container(s), disabled");
            return;
        }
        solver = new CraftingSolver(grid, result, config.GridWidth, config.GridHeight,
            supportedRecipeTypes, MarkDirty);
    }

    public InventoryDataContainer Grid => solver?.Grid;
    public InventoryDataContainer Result => solver?.Result;
    public RecipeContent CurrentMatch => solver?.CurrentMatch;

    // ---- ICraftingGridHost ----

    public void OnGridChanged() => solver?.OnGridChanged();
    public void OnResultTaken() => solver?.OnResultTaken();

    // Panel open/close actions: DescribePanel refreshes the preview on open
    // (over persisted grid materials) and chains ClearPreview as the close
    // action. Both are safe before solver assembly (null-conditional).
    public void RefreshPreview() => solver?.RefreshPreview();
    public void ClearPreview() => solver?.ClearPreview();

    // ---- panel self-report ----

    // Workbench panel: one contribution per grid container cell plus the
    // result slot, each with its owner-carrying accessor; slot codes come from
    // the containers' declared tables (P2). The preview refresh on open and
    // clear on close live here (the former BuildCraftingModel duties).
    public override void DescribePanel(PanelBuildContext ctx)
    {
        if(Grid == null || Result == null)return;   // unbound container: contributes nothing
        ctx.AddSlots(Grid, Grid.Inv.MaxSlotCount, i => new CraftingGridSlotAccess(Grid, i, this));
        ctx.AddSlot(Result, 0, new CraftingResultSlotAccess(Result, 0, this));
        ctx.AddOnClose(ClearPreview);
        RefreshPreview();   // open action: rebuild the live preview over persisted grid materials
    }

    // Instant crafting: no periodic work; Tick stays empty to keep the
    // container pipeline uniform (BlockEntityManager ticks at 20 Hz).
    public override void Tick() { }

    // ---- persistence ----

    // State fully derives from the grid data container, nothing to save.
    public override string Serialize() => "{}";

    // Data containers restore before work containers (BlockEntityManager), so
    // the grid is already loaded: drop any result that got saved while a
    // preview sat in the slot - it never represents consumed materials.
    public override void Deserialize(string json) => solver?.ClearPreview();
}

// Grid slot accessor: after every successful click/quick-move settlement on
// the grid, re-run the crafting match inside the same synchronous click flow.
public class CraftingGridSlotAccess : ContainerSlotAccess
{
    private readonly ICraftingGridHost owner;

    public CraftingGridSlotAccess(InventoryDataContainer container, int index, ICraftingGridHost owner)
        : base(container, index)
    {
        this.owner = owner;
    }

    public override void MarkChanged()
    {
        base.MarkChanged();
        owner?.OnGridChanged();
    }
}

// Result slot accessor: emptying the slot consumes the previewed recipe set.
// Policies stay on the base (the result container is Module-insert only), so
// players can only ever take from it. Half-take semantics for multi-item
// outputs are left for later - demo recipe outputs all amount to 1.
public class CraftingResultSlotAccess : ContainerSlotAccess
{
    private readonly ICraftingGridHost owner;

    public CraftingResultSlotAccess(InventoryDataContainer container, int index, ICraftingGridHost owner)
        : base(container, index)
    {
        this.owner = owner;
    }

    public override void MarkChanged()
    {
        base.MarkChanged();
        owner?.OnResultTaken();
    }
}
