using System.Collections.Generic;

// Pure view contract of an opened block-entity panel (the BE open-panel data
// starts from here, rule R-C1-4: the UI holds no BlockEntity reference). The
// slot order is the canonical order shared by the UI layout and the command
// processor's address resolution - one model per open session.
public class PanelModel
{
    public int ModelId { get; internal set; }
    // Session invalidation flag (reserved for the v1 boundary where a BE is
    // destroyed under an open panel; sync keeps running, display stalls).
    public bool Valid { get; internal set; } = true;
    public List<PanelSlotView> Slots { get; } = new();
    public FurnaceProgressView Progress { get; set; }   // furnace panels only, else null
}

// One slot's view binding inside a panel: display reads Mirror.SlotIndex,
// interaction address = ModelId + list position.
public class PanelSlotView
{
    public ContainerMirror Mirror;
    public int SlotIndex;          // index inside the mirror (containers with capacity > 1)
    public PanelSlotRole Role;     // accessor type resolved from this (see resolution table)
}

// Slot role: maps 1:1 onto the ISlotAccess subclasses the command processor
// resolves for a panel slot (Plain -> ContainerSlotAccess, crafting roles ->
// the owner-carrying accessors of the grid/result host).
public enum PanelSlotRole { Bag, CraftGrid, CraftResult, Plain }

// Furnace work-progress mirror: per-frame value copy of the four tick ints of
// ProcessingWorkContainer; ratio math stays on the UI side.
public class FurnaceProgressView
{
    public int Version { get; private set; }
    public int FuelLeftTickTime, CurrentFuelTotalTicks, CurrentTickProgress, TotalTickTime;

    public void Apply(ProcessingWorkContainer src)
    {
        if(src == null)return;
        if(FuelLeftTickTime == src.FuelLeftTickTime
           && CurrentFuelTotalTicks == src.CurrentFuelTotalTicks
           && CurrentTickProgress == src.CurrentTickProgress
           && TotalTickTime == src.TotalTickTime)return;
        FuelLeftTickTime = src.FuelLeftTickTime;
        CurrentFuelTotalTicks = src.CurrentFuelTotalTicks;
        CurrentTickProgress = src.CurrentTickProgress;
        TotalTickTime = src.TotalTickTime;
        Version++;
    }
}
