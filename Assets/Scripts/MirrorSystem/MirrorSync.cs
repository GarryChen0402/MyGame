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
        PlayerCraftGridMirror = AddContainerBinding(p.CraftingGrid.Inv);
        PlayerCraftResultMirror = AddContainerBinding(p.CraftingResult.Inv);
        PlayerHeldMirror = new HeldMirror();
        bindings.Add(new HeldBinding { Mirror = PlayerHeldMirror, Source = p.CursorStack });
    }

    // ---- panel session bindings (ContainerCommandProcessor.OpenPanel calls) ----

    // Registers one Inventory as a mirrored source (player panels register
    // resident bindings above; block-entity panel containers register here and
    // live exactly as long as the open session).
    public ContainerMirror AddContainerBinding(Inventory source)
    {
        var mirror = new ContainerMirror(source != null ? source.MaxSlotCount : 0);
        bindings.Add(new ContainerBinding { Mirror = mirror, Source = source });
        return mirror;
    }

    public void RemoveContainerBindings(IEnumerable<ContainerMirror> mirrors)
    {
        if(mirrors == null)return;
        foreach(var mirror in mirrors)
        {
            if(mirror == null)continue;
            bindings.RemoveAll(b => b is ContainerBinding cb && cb.Mirror == mirror);
        }
    }

    // Registers the furnace progress mirror source (same session lifetime as
    // the panel container bindings above).
    public FurnaceProgressView AddProgressBinding(ProcessingWorkContainer source)
    {
        var view = new FurnaceProgressView();
        bindings.Add(new ProgressBinding { View = view, Source = source });
        return view;
    }

    public void RemoveProgressBinding(FurnaceProgressView view)
    {
        if(view == null)return;
        bindings.RemoveAll(b => b is ProgressBinding pb && pb.View == view);
    }

    // ---- bindings (the only place a logic reference may live; never exposed to UI) ----

    private abstract class Binding
    {
        public abstract void Sync();
    }

    // Player backpack sources are raw Inventory; block-entity container
    // sources are container.Inv. Both read the same way.
    private class ContainerBinding : Binding
    {
        public ContainerMirror Mirror;
        public Inventory Source;

        public override void Sync()
        {
            if(Mirror == null)return;
            if(Source == null)
            {
                for(int i = 0; i < Mirror.Capacity; i++)Mirror.Apply(i, 0, 0);
                Mirror.CommitChanged();
                return;
            }
            for(int i = 0; i < Mirror.Capacity; i++)
            {
                var stack = Source.GetItemStackAt(i);
                Mirror.Apply(i, stack?.itemId ?? 0, stack?.amount ?? 0);
            }
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

    private class ProgressBinding : Binding
    {
        public FurnaceProgressView View;
        public ProcessingWorkContainer Source;

        public override void Sync() => View?.Apply(Source);
    }
}
