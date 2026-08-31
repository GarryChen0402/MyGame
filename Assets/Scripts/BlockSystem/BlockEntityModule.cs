using UnityEngine;

// Abstract base for every block entity module. Behavior modules (processing,
// crafting) reference sibling InventoryModules by name; InventoryModule is the
// only data carrier. Custom mods inherit this base and register a
// BlockEntityModuleDefinition factory; the core drives lifecycle, tick and
// serialization without knowing the concrete module type.
public abstract class BlockEntityModule
{
    public BlockEntity Host;              // position / chunk / MarkDirty entry
    public ModuleDefinition Config;       // parameters from the owning BE definition

    public virtual bool NeedsTick => false;   // only behavior modules tick

    public virtual void Tick(float deltaTime) { }
    public virtual void OnPlaced() { }        // resolve & cache inventory references
    public virtual void OnRemoved() { }       // block broken (drop items etc.)
    public virtual string SerializeToJson() => "{}";
    public virtual void DeserializeFromJson(string json) { }

    // Data changed: mark the host chunk dirty so autosave/quit flush persist it.
    protected void MarkDirty() => Host?.MarkDirty();

    protected T GetModule<T>(string name) where T : BlockEntityModule
        => Host?.GetModule<T>(name) as T;
}
