using System.Collections.Generic;
using UnityEngine;

// Slot addressing (Phase C rule R-C1-0b): the identity of every interactive
// UI slot, resolved by ContainerCommandProcessor into an ISlotAccess at
// command time. scopes: resident PlayerInventory / PlayerCrafting, or a
// block-entity panel session (panelModelId = PanelData.SessionId, slot =
// PanelData.Slots index - the canonical order shared with the UI).
public enum SlotScope { None, PlayerInventory, PlayerCrafting, Panel }

public struct SlotAddr
{
    public SlotScope scope;
    public int panelModelId;   // valid when scope == Panel
    public int slot;           // canonical slot index
}

// One open block-entity panel session (internal to the command processor):
// pre-resolved slot accessors, the session's data packet and the panel close
// action.
public class ContainerPanelSession
{
    public readonly List<ISlotAccess> Accessors = new();
    public PanelData Data;
    public System.Action OnClose;
}

// Logic-side entry of every container interaction (rule R-C1-0b): the UI
// never settles slots itself - clicks / shift-clicks / drags become commands
// that this processor resolves and executes immediately on the click frame
// (slot transactions are instant discrete ops, Phase C decision). The
// SlotClickProcessor resolution chain and the ISlotAccess family are reused
// verbatim (settlement semantics unchanged); only the call site moved.
public class ContainerCommandProcessor
{
    public static ContainerCommandProcessor Instance { get; } = new();

    private readonly Dictionary<int, ContainerPanelSession> sessions = new();
    private int nextSessionId = 1;

    // Open panel that shift-moves from the backpack target (the former
    // currentUI.ContainerSlots equivalent): PlayerCrafting while the player
    // 2x2 panel is open, Panel while a BE panel session is open, None
    // otherwise (widget test / editor panels have no container slots).
    private SlotScope activeScope;
    private int activeSessionId;

    // ---- slot settlements (instant; SCP semantics untouched) ----

    // Click / right-click command. Returns whether the cursor ended up
    // holding items - the drag gesture uses this to decide whether a press
    // picked the source slot up (mirror sync runs next frame, so the UI
    // cannot read the outcome from a mirror on the click frame itself).
    public bool Click(SlotAddr addr, bool rightClick)
    {
        SlotClickProcessor.Click(Resolve(addr), rightClick, Cursor());
        return !Cursor().IsEmpty();
    }

    public void QuickMove(SlotAddr source)
    {
        var access = Resolve(source);
        if(access == null)return;
        SlotClickProcessor.QuickMove(access, ShiftMoveTargets());
    }

    // Drag hover check (non-mutating query): mirrors deliberately carry no
    // policy (rule R-C1-1), so receive checks must ask the logic side.
    public bool CanReceive(SlotAddr target)
        => SlotClickProcessor.CanReceive(Resolve(target), Cursor());

    // Drag settlement over the collected target addresses (collection order
    // preserved); an empty list keeps the items on the cursor (legacy
    // linger semantics).
    public void DragEnd(IReadOnlyList<SlotAddr> targets, bool rightDrag)
    {
        if(targets == null || targets.Count == 0)return;
        var accesses = new List<ISlotAccess>(targets.Count);
        foreach(var t in targets)
        {
            var a = Resolve(t);
            if(a != null)accesses.Add(a);
        }
        SlotClickProcessor.DragEnd(accesses, rightDrag, Cursor());
    }

    // ---- address resolution (accessors rebuilt per command; stateless) ----

    // Resolution table: PlayerInventory -> PlayerSlotAccess (no policy);
    // PlayerCrafting grid/result -> the owner-carrying crafting accessors;
    // Panel slots -> the session accessors pre-resolved by role at open time
    // (Plain = ContainerSlotAccess, CraftGrid/CraftResult = owner accessors).
    private ISlotAccess Resolve(SlotAddr addr)
    {
        switch(addr.scope)
        {
            case SlotScope.PlayerInventory:
            {
                var p = Player.Instance;
                if(p?.inventory == null)return null;
                return new PlayerSlotAccess(p.inventory, addr.slot);
            }
            case SlotScope.PlayerCrafting:
            {
                var p = Player.Instance;
                if(p?.CraftingGrid == null || p.CraftingResult == null)return null;
                int grid = p.CraftingGrid.Inv.MaxSlotCount;
                if(addr.slot < grid)return new CraftingGridSlotAccess(p.CraftingGrid, addr.slot, p);
                if(addr.slot == grid)return new CraftingResultSlotAccess(p.CraftingResult, 0, p);
                return null;
            }
            case SlotScope.Panel:
            {
                if(!sessions.TryGetValue(addr.panelModelId, out var s))return null;
                if(addr.slot < 0 || addr.slot >= s.Accessors.Count)return null;
                return s.Accessors[addr.slot];
            }
            default: return null;
        }
    }

    // Shift-click targets of a backpack slot: the open panel's reachable
    // slots in canonical order. The SCP direction branch decides the rest:
    // container-family sources (ContainerSlotAccess and subclasses) move into
    // the backpack and ignore this list.
    private IReadOnlyList<ISlotAccess> ShiftMoveTargets()
    {
        switch(activeScope)
        {
            case SlotScope.PlayerCrafting:
            {
                var p = Player.Instance;
                if(p?.CraftingGrid == null || p.CraftingResult == null)return null;
                int grid = p.CraftingGrid.Inv.MaxSlotCount;
                var list = new List<ISlotAccess>(grid + 1);
                for(int i = 0; i < grid; i++)list.Add(new CraftingGridSlotAccess(p.CraftingGrid, i, p));
                list.Add(new CraftingResultSlotAccess(p.CraftingResult, 0, p));
                return list;
            }
            case SlotScope.Panel:
            {
                if(!sessions.TryGetValue(activeSessionId, out var s))return null;
                return s.Accessors;
            }
            default: return null;
        }
    }

    private static ItemStack Cursor() => Player.Instance.CursorStack;

    // ---- panel session lifecycle (BE open/close chain, design §6) ----

    // Session factory for a block-entity panel: registers the PanelData
    // bindings, assembles the value-only data packet and pre-resolves the
    // slot accessors by work-container type. The BE itself never reaches the
    // UI - the packet is the whole data contract (rule R-C1-4).
    public PanelData OpenPanel(BlockEntity be)
    {
        var session = new ContainerPanelSession();
        if(be != null)
        {
            bool built = false;
            foreach(var wc in be.WorkContainers)
            {
                if(wc is ProcessingWorkContainer pwc && !built)
                {
                    built = BuildFurnaceModel(session, pwc);
                }
                else if(wc is CraftingWorkContainer cwc && !built)
                {
                    built = BuildCraftingModel(session, cwc);
                }
            }
            if(!built)Debug.LogWarning($"[ContainerCommandProcessor] no matching work container for panel of {be.Definition?.FullName}; opened an empty data packet");
        }
        if(session.Data == null)session.Data = new PanelData(nextSessionId++, 0, 0);
        sessions[session.Data.SessionId] = session;
        activeScope = SlotScope.Panel;
        activeSessionId = session.Data.SessionId;
        return session.Data;
    }

    // Furnace: canonical slot order 0 = input, 1 = fuel, 2 = output (each a
    // capacity-1 container) + the four tick counters as channels. No open
    // action.
    private bool BuildFurnaceModel(ContainerPanelSession session, ProcessingWorkContainer pwc)
    {
        if(pwc.Input == null || pwc.Fuel == null || pwc.Output == null)return false;
        var data = new PanelData(nextSessionId++, 3, 4);
        MirrorSync.Instance.AddPanelSlotBinding(data, 0, pwc.Input.Inv);
        MirrorSync.Instance.AddPanelSlotBinding(data, 1, pwc.Fuel.Inv);
        MirrorSync.Instance.AddPanelSlotBinding(data, 2, pwc.Output.Inv);
        MirrorSync.Instance.AddPanelChannelBinding(data, 0, pwc);
        session.Accessors.Add(new ContainerSlotAccess(pwc.Input, 0));
        session.Accessors.Add(new ContainerSlotAccess(pwc.Fuel, 0));
        session.Accessors.Add(new ContainerSlotAccess(pwc.Output, 0));
        session.Data = data;
        return true;
    }

    // Workbench: canonical slot order = grid cells 0..8, result = 9. The
    // preview refresh on open and clear on close moved here from the UI panel
    // (design §6.1/§6.2).
    private bool BuildCraftingModel(ContainerPanelSession session, CraftingWorkContainer cwc)
    {
        if(cwc.Grid == null || cwc.Result == null)return false;
        int grid = cwc.Grid.Inv.MaxSlotCount;
        var data = new PanelData(nextSessionId++, grid + 1, 0);
        MirrorSync.Instance.AddPanelSlotBinding(data, 0, cwc.Grid.Inv);
        MirrorSync.Instance.AddPanelSlotBinding(data, grid, cwc.Result.Inv);
        for(int i = 0; i < grid; i++)session.Accessors.Add(new CraftingGridSlotAccess(cwc.Grid, i, cwc));
        session.Accessors.Add(new CraftingResultSlotAccess(cwc.Result, 0, cwc));
        session.OnClose = () => cwc.ClearPreview();
        session.Data = data;

        cwc.RefreshPreview();   // open action: rebuild the live preview over persisted grid materials
        return true;
    }

    // Panel close command (fired by UIManager.CloseUI before hiding): runs
    // the session close action (workbench preview clear), drops the data
    // bindings and clears the active shift-move target.
    public void ClosePanel(int sessionId)
    {
        if(sessionId == 0 || !sessions.TryGetValue(sessionId, out var s))return;
        s.OnClose?.Invoke();
        sessions.Remove(sessionId);
        MirrorSync.Instance.RemovePanelBindings(s.Data);
        if(activeSessionId == sessionId)
        {
            activeSessionId = 0;
            activeScope = SlotScope.None;
        }
    }

    // Player 2x2 panel open (PlayerInputHandler route; the preview refresh
    // that PlayerUI.OnEnable used to run on open).
    public void OpenPlayerInventory()
    {
        Player.Instance.Crafting?.RefreshPreview();
        activeScope = SlotScope.PlayerCrafting;
        activeSessionId = 0;
        UIManager.Instance?.OpenUI(PlayerUI.playerUIDefinition.FullName, null);
    }

    // ---- debug give (WidgetTestUI button route) ----

    // Grants `amount` of the named item in MaxStack chunks; stops when the
    // backpack cannot hold more (same settlement the panel used to run).
    public void DebugGiveItem(string fullName, int amount)
    {
        if(amount <= 0)return;
        var rs = ResourceSystem.Instance;
        if(!rs.ItemDefinitions.TryGetResourceWithFullName(fullName, out var def))return;
        if(!rs.ItemDefinitions.TryGetNumberId(fullName, out ushort id))return;
        var inventory = Player.Instance?.inventory;
        if(inventory == null)return;

        int remaining = amount;
        while(remaining > 0)
        {
            int piece = Mathf.Min(remaining, def.MaxStack);
            var stack = new ItemStack { itemId = id, amount = piece };
            if(!inventory.TryAddItemStack(stack))break;   // full: grant what fits, drop the rest
            remaining -= piece;
        }
        int granted = amount - remaining;
        Debug.Log($"WidgetTest: granted {granted}/{amount} {def.FullName}");
    }
}
