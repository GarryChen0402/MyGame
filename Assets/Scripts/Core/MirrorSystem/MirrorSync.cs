using System.Collections.Generic;

// Mirror sync singleton (rule R-C1-0): the only writer of every mirror. The
// UI reads mirrors; logic declares binding sources via the Register* methods
// below. Sync() runs once per render frame right after the game-logic ticks
// (GameLoopDriver.Update tail); in a process-split future this call is the
// network sync point. A Sync() over an empty binding table is a no-op, so the
// UI must null-guard its mirror getters until the player sources register.
public class MirrorSync
{
    public static MirrorSync Instance { get; } = new();

    private readonly List<Binding> bindings = new();

    // ---- resident player mirrors (UI read side) ----

    public ContainerMirror PlayerInventoryMirror { get; private set; }   // 36
    public ContainerMirror PlayerCraftGridMirror { get; private set; }   // 4
    public ContainerMirror PlayerCraftResultMirror { get; private set; } // 1
    public HeldMirror PlayerHeldMirror { get; private set; }
    public PlayerMirror PlayerMirror { get; private set; }               // camera read side (rule R-C2-5)

    // ---- per-render-frame sync point (GameLoopDriver.Update tail) ----

    public void Sync()
    {
        foreach(var b in bindings)b.Sync();
    }

    // ---- resident player bindings ----

    // Called from the tail of Player's static ctor, so the binding happens at
    // exactly the current Player.Instance creation point - never earlier
    // (creating Player prematurely would fire SummonEntityEvent before the
    // logic managers subscribe and lose it).
    public void RegisterPlayerSources()
    {
        var p = Player.Instance;
        PlayerInventoryMirror = AddContainerBinding(p.inventory);
        PlayerCraftGridMirror = AddContainerBinding(p.CraftingGrid);
        PlayerCraftResultMirror = AddContainerBinding(p.CraftingResult);
        PlayerHeldMirror = new HeldMirror();
        bindings.Add(new HeldBinding { Mirror = PlayerHeldMirror, Source = p.CursorStack });
        PlayerMirror = new PlayerMirror();
        bindings.Add(new PlayerBinding { Mirror = PlayerMirror, Source = p });
    }

    // ---- entity mirrors (logic spawn entries register; rule R-C1-0c/R-C2-1) ----

    // Called by the logic spawn entry points once the entity's data is
    // complete (mob Init / item placement); the returned mirror rides the
    // EntityShellSpawnEvent to the shell manager.
    public EntityMirror RegisterEntityMirror(Entity entity)
    {
        var mirror = new EntityMirror(entity.EntityId);
        bindings.Add(new EntityBinding { Mirror = mirror, Source = entity });
        mirror.ApplyFrom(entity);   // initial snapshot: ready before any shell binds
        return mirror;
    }

    // Called from Entity.OnDestroy; the shell manager removes its GO through
    // the EntityShellDespawnEvent published alongside.
    public void UnregisterEntityMirror(int entityId)
    {
        bindings.RemoveAll(b => b is EntityBinding eb && eb.Mirror != null && eb.Mirror.EntityId == entityId);
    }

    // ---- standalone container bindings (resident player sources) ----

    // Container-level binding (P2/P4): the container doubles as the pack
    // source, so its resident mirror carries the packed snapshot alongside
    // the value slots (player backpack, craft grid/result); block-entity
    // panel containers go through the PanelData bindings below instead.
    public ContainerMirror AddContainerBinding(InventoryDataContainer container)
        => AddContainerBinding(container?.Inv, container);

    private ContainerMirror AddContainerBinding(Inventory source, DataContainer packSource)
    {
        var mirror = new ContainerMirror(source != null ? source.MaxSlotCount : 0);
        bindings.Add(new ContainerBinding { Mirror = mirror, Source = source, PackSource = packSource });
        return mirror;
    }

    // Registers the work-container panel contributions into the session's
    // data packet: slot contributions merge into runs over the same container
    // (one binding copies a whole consecutive run per sync pass), channels
    // lay out in contribution order. Each container's first run also owns the
    // container's pack entry (P2, D3): the run rebuilds the pack when its
    // value pass changed something. The session owns the binding lifetime
    // (RemovePanelBindings).
    public void AddPanelBindings(PanelData data, IReadOnlyList<PanelSlotSource> slots, IReadOnlyList<ChannelSource> channels)
    {
        if(data == null)return;
        if(slots != null)
        {
            int runStart = 0;
            int packIndex = -1;
            InventoryDataContainer runContainer = null;
            while(runStart < slots.Count)
            {
                var first = slots[runStart];
                if(first.Container != runContainer)
                {
                    runContainer = first.Container;
                    packIndex++;
                }
                int runEnd = runStart + 1;
                while(runEnd < slots.Count
                    && slots[runEnd].Container == runContainer
                    && slots[runEnd].SourceIndex == first.SourceIndex + (runEnd - runStart))
                    runEnd++;
                bindings.Add(new PanelSlotBinding
                {
                    Data = data,
                    Start = runStart,
                    Container = runContainer,
                    SourceStart = first.SourceIndex,
                    PackIndex = packIndex,
                    Count = runEnd - runStart
                });
                runStart = runEnd;
            }
        }
        if(channels != null)
        {
            int start = 0;
            foreach(var channel in channels)
            {
                bindings.Add(new PanelChannelBinding { Data = data, Start = start, Source = channel.Source });
                start += channel.Names.Length;
            }
        }
    }

    // Drops every binding that writes into this session's data packet.
    public void RemovePanelBindings(PanelData data)
    {
        if(data == null)return;
        bindings.RemoveAll(b =>
            (b is PanelSlotBinding sb && sb.Data == data) ||
            (b is PanelChannelBinding cb && cb.Data == data));
    }

    // ---- bindings (the only place a logic reference may live; never exposed to UI) ----

    private abstract class Binding
    {
        public abstract void Sync();
    }

    // Resident sources copy from the container's raw Inventory; every
    // container-level source also carries the pack contract (PackSource) so
    // the mirror gets a packed snapshot alongside the values.
    private class ContainerBinding : Binding
    {
        public ContainerMirror Mirror;
        public Inventory Source;
        public DataContainer PackSource;

        public override void Sync()
        {
            if(Mirror == null)return;
            bool valueChanged = false;
            if(Source == null)
            {
                for(int i = 0; i < Mirror.Capacity; i++)valueChanged |= Mirror.Apply(i, 0, 0);
            }
            else
            {
                for(int i = 0; i < Mirror.Capacity; i++)
                {
                    var stack = Source.GetItemStackAt(i);
                    valueChanged |= Mirror.Apply(i, stack?.itemId ?? 0, stack?.amount ?? 0);
                }
            }
            // D5: rebuild the pack only when the value pass changed something
            // (or before the first pack exists), so a static container never
            // re-forms its string every frame.
            if(PackSource != null && (valueChanged || Mirror.Pack == null))
                Mirror.ApplyPack(PackSource.GetPackData());
            Mirror.CommitChanged();
        }
    }

    // The cursor stack object never gets replaced (Player.CursorStack is
    // mutated in place by settlements), so holding the object is stable.
    private class HeldBinding : Binding
    {
        public HeldMirror Mirror;
        public ItemStack Source;

        public override void Sync() => Mirror?.Apply(Source);
    }

    // Panel path: many containers write into one shared value packet. One
    // binding copies a consecutive run of panel slots from a consecutive run
    // of container cells; the declared run length wins over a larger source
    // container. The binding doubles as the pack rebuild point of its run
    // (P2, D3/D5): the container's pack contract re-forms the text only when
    // the value pass changed something (or before the first pack exists).
    private class PanelSlotBinding : Binding
    {
        public PanelData Data;
        public int Start;        // first panel slot of the run
        public InventoryDataContainer Container;
        public int SourceStart;  // first source cell copied
        public int PackIndex;    // pack entry of the session this run owns
        public int Count;        // cells in the run

        public override void Sync()
        {
            if(Data == null)return;
            var source = Container?.Inv;
            bool valueChanged = false;
            if(source == null)
            {
                int last = System.Math.Min(Start + Count, Data.Capacity);
                for(int i = Start; i < last; i++)valueChanged |= Data.ApplySlot(i, 0, 0);
            }
            else
            {
                int end = System.Math.Min(System.Math.Min(Count, source.MaxSlotCount - SourceStart), Data.Capacity - Start);
                for(int i = 0; i < end; i++)
                {
                    var stack = source.GetItemStackAt(SourceStart + i);
                    valueChanged |= Data.ApplySlot(Start + i, stack?.itemId ?? 0, stack?.amount ?? 0);
                }
            }
            if(Container != null && (valueChanged || !Data.HasPack(PackIndex)))
                Data.SetPack(PackIndex, Container.GetPackData());
            Data.CommitChanged();
        }
    }

    private class PanelChannelBinding : Binding
    {
        public PanelData Data;
        public int Start;
        public IChannelSource Source;

        public override void Sync()
        {
            if(Data == null || Source == null)return;
            Source.ReadChannels(Data, Start);
            Data.CommitChanged();
        }
    }

    // PlayerMirror source: the player singleton (never replaced in v1).
    private class PlayerBinding : Binding
    {
        public PlayerMirror Mirror;
        public Player Source;

        public override void Sync() => Mirror?.ApplyFrom(Source);
    }

    // One entity mirror per live logic entity; entries live exactly as long
    // as the entity (RegisterEntityMirror/UnregisterEntityMirror).
    private class EntityBinding : Binding
    {
        public EntityMirror Mirror;
        public Entity Source;

        public override void Sync() => Mirror?.ApplyFrom(Source);
    }
}
