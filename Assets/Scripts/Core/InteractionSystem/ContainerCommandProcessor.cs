using System.Collections.Generic;
using UnityEngine;

// Slot addressing (Phase C rule R-C1-0b): the identity of every interactive
// UI slot, resolved by ContainerCommandProcessor into an ISlotAccess at
// command time. scopes: resident PlayerInventory / PlayerCrafting, or a
// block-entity panel session (beId = PanelData.BeId, the deterministic BE
// address; slot = PanelData.Slots index - the canonical order shared with
// the UI).
public enum SlotScope { None, PlayerInventory, PlayerCrafting, Panel }

public struct SlotAddr
{
    public SlotScope scope;
    public string beId;        // valid when scope == Panel (BlockEntityId.Of)
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

    private readonly Dictionary<string, ContainerPanelSession> sessions = new();

    // Open panel that shift-moves from the backpack target (the former
    // currentUI.ContainerSlots equivalent): PlayerCrafting while the player
    // 2x2 panel is open, Panel while a BE panel session is open, None
    // otherwise (widget test / editor panels have no container slots).
    private SlotScope activeScope;
    private string activeBeId = string.Empty;   // valid when activeScope == Panel

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
    // Panel slots -> the session accessors self-reported by the work
    // containers at open time.
    private ISlotAccess Resolve(SlotAddr addr)
    {
        switch(addr.scope)
        {
            case SlotScope.PlayerInventory:
            {
                var p = Player.Instance;
                if(p?.inventory == null)return null;
                return new PlayerSlotAccess(p.inventory.Inv, addr.slot);
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
                if(!sessions.TryGetValue(addr.beId, out var s))return null;
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
                if(!sessions.TryGetValue(activeBeId, out var s))return null;
                return s.Accessors;
            }
            default: return null;
        }
    }

    private static ItemStack Cursor() => Player.Instance.CursorStack;

    // ---- panel session lifecycle (BE open/close chain, design §6) ----

    // Session factory for a block-entity panel: the work containers self-report
    // their panel (names + accessors + close actions) into a build context, and
    // the contributions are flattened into the value-only data packet and
    // registered as mirror fill bindings (S2 of Docs/改造提案-UI数据流拆分方案.md;
    // no per-device dispatch left). The BE itself never reaches the UI - the
    // packet is the whole data contract (rule R-C1-4).
    public PanelData OpenPanel(BlockEntity be)
    {
        string beId = BlockEntityId.Of(be);
        var ctx = new PanelBuildContext();
        if(be != null)
        {
            foreach(var wc in be.WorkContainers)wc.DescribePanel(ctx);
            if(ctx.SlotSources.Count == 0)
                Debug.LogWarning($"[ContainerCommandProcessor] no work container described a panel for {be.Definition?.FullName}; opened an empty data packet");
        }
        var data = new PanelData(beId, ctx.SlotNames, ctx.ChannelNames);
        var session = new ContainerPanelSession { Data = data, OnClose = ctx.OnClose };
        foreach(var slot in ctx.SlotSources)session.Accessors.Add(slot.Accessor);
        MirrorSync.Instance.AddPanelBindings(data, ctx.SlotSources, ctx.ChannelSources);
        sessions[beId] = session;
        activeScope = SlotScope.Panel;
        activeBeId = beId;
        return data;
    }

    // Panel close command (fired by UIManager.CloseUI before hiding): runs
    // the session close action (workbench preview clear), drops the data
    // bindings and clears the active shift-move target.
    public void ClosePanel(string beId)
    {
        if(string.IsNullOrEmpty(beId) || !sessions.TryGetValue(beId, out var s))return;
        s.OnClose?.Invoke();
        sessions.Remove(beId);
        MirrorSync.Instance.RemovePanelBindings(s.Data);
        if(activeBeId == beId)
        {
            activeBeId = string.Empty;
            activeScope = SlotScope.None;
        }
    }

    // Player 2x2 panel open (PlayerInputHandler route; the preview refresh
    // that PlayerUI.OnEnable used to run on open).
    public void OpenPlayerInventory()
    {
        Player.Instance.Crafting?.RefreshPreview();
        activeScope = SlotScope.PlayerCrafting;
        activeBeId = string.Empty;
        UIManager.Instance?.OpenUI(PlayerUI.playerUIDefinition.FullName, null);
    }

    // ---- give (cheat-mode grant; WidgetTestUI + JEI routes) ----

    // Grants `amount` of the named item in MaxStack chunks; stops when the
    // backpack cannot hold more (same settlement the panel used to run).
    // Returns how many were actually granted (legacy callers ignore it; the
    // /give command echoes it).
    public int GiveItem(string fullName, int amount)
    {
        if(amount <= 0)return 0;
        var rs = ResourceSystem.Instance;
        if(!rs.ItemDefinitions.TryGetResourceWithFullName(fullName, out var def))return 0;
        if(!rs.ItemDefinitions.TryGetNumberId(fullName, out ushort id))return 0;
        var inventory = Player.Instance?.inventory?.Inv;
        if(inventory == null)return 0;

        int remaining = amount;
        while(remaining > 0)
        {
            int piece = Mathf.Min(remaining, def.MaxStack);
            var stack = new ItemStack { itemId = id, amount = piece };
            if(!inventory.TryAddItemStack(stack))break;   // full: grant what fits, drop the rest
            remaining -= piece;
        }
        int granted = amount - remaining;
        Debug.Log($"[GiveItem] granted {granted}/{amount} {def.FullName}");
        return granted;
    }
}
