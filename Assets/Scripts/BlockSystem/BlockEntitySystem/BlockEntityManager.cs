using System.Collections.Generic;
using UnityEngine;

// Ticks every active block entity on a fixed 20Hz game step. Listens for block
// edits: placing a block with HasBlockEntity assembles its BE through the
// BlockEntityDefinition factory and stores it in the chunk via
// Chunk.RegisterBlockEntity; breaking/replacing a block drops the stale BE via
// Chunk.RemoveBlockEntity. On chunk unload / disable every BE of that chunk is
// unregistered (Chunk.UnregisterAllBlockEntities) so no orphan keeps ticking.
public class BlockEntityManager
{
    public static BlockEntityManager Instance { get; } = new();

    // One Tick() call on a work container == one game tick (MC semantics).
    private const float TickInterval = 1f / 20f;

    private readonly HashSet<BlockEntity> tracked = new();
    private readonly List<BlockEntity> tickBuffer = new();   // snapshot for iteration
    private float accumulator;

    // Touched by GameBootstrap (Phase1) before any chunk event can fire.
    private BlockEntityManager()
    {
        EventBus.Instance.Subscribe<BlockChangedEvent>(OnBlockChanged);
        EventBus.Instance.Subscribe<ChunkUnloadedEvent>(OnChunkUnloaded);
        EventBus.Instance.Subscribe<ChunkLoadedEvent>(OnChunkLoaded);
    }

    public void Register(BlockEntity be)
    {
        if(be == null)return;
        if(!tracked.Add(be))
            Debug.LogWarning($"[BlockEntityManager] block entity already tracked ({be.Definition?.FullName} at {be.Position})");
    }

    // Stops ticking the BE; its work containers get their OnRemoved hook (e.g.
    // dropping inventory on break - currently no-op until implemented).
    public void Unregister(BlockEntity be)
    {
        if(be == null || !tracked.Remove(be))return;
        foreach(var wc in be.WorkContainers)wc.OnRemoved();
    }

    // Fixed-step accumulator, driven from WorldRenderer.Update like
    // WorldSaveManager.Instance.Tick. If the frame rate drops below 20fps the
    // while-loop catches up the backlog so game ticks never lag wall time.
    public void Tick(float deltaTime)
    {
        accumulator += deltaTime;
        while(accumulator >= TickInterval)
        {
            accumulator -= TickInterval;
            TickOnce();
        }
    }

    private void TickOnce()
    {
        if(tracked.Count == 0)return;
        tickBuffer.Clear();
        tickBuffer.AddRange(tracked);   // snapshot: unregister mid-tick can't break iteration
        foreach(var be in tickBuffer)be.TickWorkContainers();
    }

    // Makes the chunk's BE at this spot match the new block: broken (state 0)
    // or replaced blocks drop the stale BE; blocks with HasBlockEntity spawn
    // their BE from the registered BlockEntityDefinition factory.
    private void OnBlockChanged(BlockChangedEvent evt)
    {
        var chunk = evt.Chunk;
        chunk.RemoveBlockEntity(evt.ChunkLocalCoord);
        if(evt.NewStateId == 0)return;

        BlockDefinition def = ResourceSystem.Instance.GetState(evt.NewStateId)?.Block;
        if(def == null || !def.HasBlockEntity || string.IsNullOrEmpty(def.BlockEntityDefinitionFullName))return;
        if(!ResourceSystem.Instance.BlockEntityDefinitions.TryGetResourceWithFullName(def.BlockEntityDefinitionFullName, out var beDef))
        {
            Debug.LogWarning($"[BlockEntityManager] unknown block entity definition '{def.BlockEntityDefinitionFullName}' for {def.FullName}");
            return;
        }
        var be = beDef.CreateNewBlockEntity(
            Chunk.ChunkLocalCoordToDimensionCoord(evt.ChunkCoord, evt.ChunkLocalCoord));
        chunk.RegisterBlockEntity(be, evt.ChunkLocalCoord);
    }

    // Chunk disabled/unloaded: its BEs stop ticking and are dropped with it.
    private void OnChunkUnloaded(ChunkUnloadedEvent evt) => evt.Chunk.UnregisterAllBlockEntities();

    // Chunk (re)loaded: rebuild every BE held in PendingBlockEntities - either
    // parsed from the save file by the load worker or cached by the unload path.
    // Restored BEs go through the normal register flow, so they tick and dirty
    // their chunk from here on.
    private void OnChunkLoaded(ChunkLoadedEvent evt)
    {
        var chunk = evt.Chunk;
        List<BlockEntitySaveData> pending = chunk.PendingBlockEntities;
        chunk.PendingBlockEntities = null;   // consumed: never restore twice
        if(pending == null || pending.Count == 0)return;
        foreach(var entry in pending)RestoreBlockEntity(chunk, entry);
    }

    // Rebuilds one BE from its save entry: data containers are deserialized by
    // name (definition changes skip with a warning), work containers by list
    // order (mirrors the config order used at assembly).
    private void RestoreBlockEntity(Chunk chunk, BlockEntitySaveData entry)
    {
        if(entry == null || string.IsNullOrEmpty(entry.type))return;
        if(!ResourceSystem.Instance.BlockEntityDefinitions.TryGetResourceWithFullName(entry.type, out var beDef))
        {
            Debug.LogWarning($"[BlockEntityManager] unknown block entity type '{entry.type}' in chunk save; skipped");
            return;
        }
        Vector3Int localCoord = new(entry.x, entry.y, entry.z);
        if(!chunk.IsCorrectChunkLocalCoord(localCoord))
        {
            Debug.LogWarning($"[BlockEntityManager] out-of-range block entity position {localCoord} in chunk save; skipped");
            return;
        }
        var be = beDef.CreateNewBlockEntity(Chunk.ChunkLocalCoordToDimensionCoord(chunk.ChunkCoord, localCoord));
        foreach(var dc in entry.data)
        {
            if(dc == null || string.IsNullOrEmpty(dc.name) || string.IsNullOrEmpty(dc.data))continue;
            if(!be.DataContainers.TryGetValue(dc.name, out var container))
            {
                Debug.LogWarning($"[BlockEntityManager] save data for unknown container '{dc.name}' of {entry.type}; skipped");
                continue;
            }
            container.Deserialize(dc.data);
        }
        int count = Mathf.Min(entry.work.Count, be.WorkContainers.Count);
        for(int i = 0; i < count; i++)
        {
            if(entry.work[i] == null || string.IsNullOrEmpty(entry.work[i].data))continue;
            be.WorkContainers[i].Deserialize(entry.work[i].data);
        }
        chunk.RegisterBlockEntity(be, localCoord);
    }
}
