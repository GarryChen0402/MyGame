using UnityEngine;

// Lifecycle + tick driver for all block entities (singleton, mounted by
// GameBootstrap). Creation/removal are event-driven: a placed block whose
// definition declares a BE gets one; a block change to air removes it.
// Chunks loaded from disk carry pending BE data (parsed on the worker) which
// is materialized here on ChunkLoaded. Tick only runs BEs with a NeedsTick
// module (Processing) in enabled chunks near the player.
public class BlockEntityManager
{
    public static BlockEntityManager Instance { get; } = new();

    // Tick radius in chunks around the player's chunk.
    public const int TickChunkRange = 4;

    public void Initialize()
    {
        EventBus.Instance.Subscribe<BlockChangedEvent>(OnBlockChanged);
        EventBus.Instance.Subscribe<ChunkLoadedEvent>(OnChunkLoaded);
    }

    // Main thread, every frame (WorldRenderer.Update).
    public void Update(float deltaTime)
    {
        var player = Player.Instance;
        if (player == null) return;
        Vector2Int playerChunk = Dimension.WorldPosToChunkCoord(player.Position);
        foreach (var dim in WorldManager.Instance.Dimensions.Values)
        {
            foreach (var chunk in dim.GetEnableChunks())
            {
                if (chunk.BlockEntities.Count == 0) continue;
                if (chunk.DistanceTo(playerChunk) > TickChunkRange) continue;
                foreach (var be in chunk.BlockEntities.Values)
                    if (!be.Removed && be.NeedsTick) be.Tick(deltaTime);
            }
        }
    }

    // Interaction entry point (right-click). Returns true when the position
    // hosts a BE, blocking block placement on it (vanilla container behavior).
    // Container GUI hooks up here in the future.
    public bool TryInteract(Player player, Vector3Int dimensionCoord)
        => GetBlockEntity(player.DimensionId, dimensionCoord) != null;

    public BlockEntity GetBlockEntity(ushort dimensionId, Vector3Int dimensionCoord)
    {
        if (!WorldManager.Instance.TryGetDimension(dimensionId, out var dim)) return null;
        var chunkCoord = Dimension.DimensionCoordToChunkCoord(dimensionCoord);
        if (!dim.TryGetChunk(chunkCoord, out var chunk)) return null;
        return chunk.BlockEntities.TryGetValue(Chunk.DimensionCoordToChunkLocalCoord(dimensionCoord), out var be) ? be : null;
    }

    // ---- lifecycle ----

    private void OnBlockChanged(BlockChangedEvent evt)
    {
        if (evt.NewStateId == 0) { RemoveBlockEntity(evt); return; }
        CreateBlockEntity(evt);
    }

    // Placed (or replaced) block with a declared BE: instantiate it via the
    // definition's factory chain and hang it on the chunk.
    private void CreateBlockEntity(BlockChangedEvent evt)
    {
        var state = ResourceSystem.Instance.GetState(evt.NewStateId);
        var blockDef = state?.Block;
        if (blockDef == null || !blockDef.HasBlockEntity || string.IsNullOrEmpty(blockDef.BlockEntityDefinitionFullName)) return;
        if (evt.Chunk == null) return;
        if (evt.Chunk.BlockEntities.ContainsKey(evt.ChunkLocalCoord)) return;   // already has one

        if (!ResourceSystem.Instance.BlockEntityDefinitions
                .TryGetResourceWithFullName(blockDef.BlockEntityDefinitionFullName, out var beDef))
        {
            Debug.LogWarning($"[BlockEntityManager] unknown BlockEntityDefinition '{blockDef.BlockEntityDefinitionFullName}' for block {blockDef.FullName}; treated as plain block");
            return;
        }
        Vector3Int dimPos = new(
            evt.Chunk.ChunkCoord.x * SubChunk.SubChunkBlockSize + evt.ChunkLocalCoord.x,
            evt.ChunkLocalCoord.y,
            evt.Chunk.ChunkCoord.y * SubChunk.SubChunkBlockSize + evt.ChunkLocalCoord.z);
        var be = beDef.CreateNewBlockEntity(dimPos, evt.NewStateId);
        be.Chunk = evt.Chunk;
        evt.Chunk.BlockEntities[evt.ChunkLocalCoord] = be;
    }

    private void RemoveBlockEntity(BlockChangedEvent evt)
    {
        if (evt.Chunk == null) return;
        if (evt.Chunk.BlockEntities.Remove(evt.ChunkLocalCoord, out var be))
            be.OnRemoved();   // per-module drops (items etc.)
    }

    // A chunk became available (freshly loaded or re-enabled): materialize any
    // BE data that the worker parsed from disk. Re-enabled pooled chunks keep
    // their in-memory BEs, so nothing to do there.
    private void OnChunkLoaded(ChunkLoadedEvent evt)
    {
        if (evt.Chunk.PendingBlockEntities == null) return;
        foreach (var data in evt.Chunk.PendingBlockEntities)
        {
            if (data == null) continue;
            var local = new Vector3Int(data.x, data.y, data.z);
            if (!evt.Chunk.IsCorrectChunkLocalCoord(local))
            {
                Debug.LogWarning($"[BlockEntityManager] block entity at ({data.x},{data.y},{data.z}) out of chunk range; dropped");
                continue;
            }
            if (!ResourceSystem.Instance.BlockEntityDefinitions.TryGetResourceWithFullName(data.type, out var beDef))
            {
                Debug.LogWarning($"[BlockEntityManager] unknown block entity type '{data.type}'; dropped");
                continue;
            }
            ushort stateId = evt.Chunk.GetBlockAt(local);
            if (stateId == 0) continue;   // host block gone; drop the orphan
            Vector3Int dimPos = new(
                evt.Chunk.ChunkCoord.x * SubChunk.SubChunkBlockSize + local.x,
                local.y,
                evt.Chunk.ChunkCoord.y * SubChunk.SubChunkBlockSize + local.z);
            var be = beDef.CreateNewBlockEntity(dimPos, stateId);
            be.Chunk = evt.Chunk;
            be.DeserializeFromSave(data.modules);
            evt.Chunk.BlockEntities[local] = be;
        }
        evt.Chunk.PendingBlockEntities = null;
    }
}
