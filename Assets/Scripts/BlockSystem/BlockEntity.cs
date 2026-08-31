using System.Collections.Generic;
using UnityEngine;

// Host object for a block entity: a dynamic-data object bound to one block
// position. Owns a list of BlockEntityModules; position/state/chunk lifecycle
// are managed by BlockEntityManager. Pure C# (no Unity object deps) so it is
// safe to create on worker threads; all data access still happens on the main
// thread.
public class BlockEntity
{
    public Vector3Int Position;            // dimension coord
    public ushort StateId;                 // host block's global state id (redundant cache)
    public BlockEntityDefinition Definition;
    public Chunk Chunk;                    // set by the manager; MarkDirty entry point
    public bool Removed { get; private set; }

    private readonly List<BlockEntityModule> modules = new();
    private readonly Dictionary<string, BlockEntityModule> modulesByName = new();

    public BlockEntity(Vector3Int position, ushort stateId, BlockEntityDefinition definition)
    {
        Position = position;
        StateId = stateId;
        Definition = definition;
    }

    public void AddModule(BlockEntityModule module)
    {
        module.Host = this;
        modules.Add(module);
        if (string.IsNullOrEmpty(module.Config?.Name)) return;
        if (modulesByName.ContainsKey(module.Config.Name))
            Debug.LogWarning($"[BlockEntity] duplicate module name '{module.Config.Name}' on {Definition.FullName}; the later one wins");
        modulesByName[module.Config.Name] = module;
    }

    // Runs every module's OnPlaced (reference resolution + caching).
    public void InitModules()
    {
        foreach (var m in modules) m.OnPlaced();
    }

    // The only way modules resolve sibling modules: by registered name.
    public T GetModule<T>(string name) where T : BlockEntityModule
        => name != null && modulesByName.TryGetValue(name, out var m) ? m as T : null;

    public bool NeedsTick
    {
        get { foreach (var m in modules) if (m.NeedsTick) return true; return false; }
    }

    // Main thread, every frame while the host chunk is enabled and near the player.
    public void Tick(float deltaTime)
    {
        for (int i = 0; i < modules.Count; i++)
            if (!Removed && modules[i].NeedsTick) modules[i].Tick(deltaTime);
    }

    // Block broken: every module drops/saves its state; Removed prevents any
    // further tick/interact. Idempotent.
    public void OnRemoved()
    {
        if (Removed) return;
        Removed = true;
        foreach (var m in modules) m.OnRemoved();
    }

    // BE data changed outside TrySetBlockAt: mark the host chunk for saving.
    public void MarkDirty() => Chunk?.MarkModifiedByBlockEntity();

    // ---- serialization (main thread only) ----

    // Snapshot: per-module JSON strings in declaration order. The worker only
    // pastes these into the chunk JSON; module data is main-thread-owned.
    public List<ModuleSaveData> BuildModuleSaveData()
    {
        var list = new List<ModuleSaveData>(modules.Count);
        foreach (var m in modules)
            list.Add(new ModuleSaveData { module = m.Config?.Name, data = m.SerializeToJson() });
        return list;
    }

    // Restore: module data is matched to BlockEntityDefinition.Modules by index
    // (declaration order); a type mismatch skips that module's data.
    public void DeserializeFromSave(List<ModuleSaveData> saveData)
    {
        if (saveData == null) return;
        for (int i = 0; i < saveData.Count && i < modules.Count; i++)
        {
            if (saveData[i] == null) continue;
            modules[i].DeserializeFromJson(saveData[i].data);
        }
    }
}
