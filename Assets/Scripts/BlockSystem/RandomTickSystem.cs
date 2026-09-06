using System.Collections.Generic;
using UnityEngine;

// MC-style random ticks (design docs 随机刻系统-规则设计.md / -代码设计.md):
// every game tick - a 20Hz accumulator, one block-domain step like
// BlockEntityManager - each non-empty subchunk is sampled
// RandomTickSpeedPerSection times at uniform random cells; a sampled block
// whose BlockDefinition declares a RandomTick delegate runs it with a
// RandomTickContext. Drives grass spread and future crop/sapling behaviors.
// No EventBus (high frequency, no subscribers; Forge has no generic random
// tick event either). Handlers touch the world through the context only,
// which never writes into unloaded chunks.
public class RandomTickSystem
{
    public static RandomTickSystem Instance { get; } = new();
    public const float TickInterval = 1f / 20f;
    public const int RandomTickSpeedPerSection = 100;   // initial demo speed; vanilla default is 3
    private static readonly int SectionVolume = SubChunk.SubChunkBlockSize * SubChunk.SubChunkBlockSize * SubChunk.SubChunkBlockSize;

    private float accumulator;

    // One RNG per dimension (rules R6): world seed mixed with dimension number.
    private readonly Dictionary<Dimension, System.Random> rngByDimension = new();

    // stateId -> RandomTick delegate. State ids are dense sequential ushorts
    // (assigned by BuildAllBlockStates), so a plain array indexed by state id
    // beats a dictionary on the millions-of-samples-per-second hot path. Built
    // on the first tick, when PostFreeze already materialized BlockStates.
    private System.Action<RandomTickContext>[] handlerByState = new System.Action<RandomTickContext>[0];

    private RandomTickSystem() { }

    // Fixed-step accumulator like BlockEntityManager: frame-rate independent,
    // and a backlog below 20fps is caught up instead of lost.
    public void Tick(float deltaTime)
    {
        accumulator += deltaTime;
        while (accumulator >= TickInterval)
        {
            accumulator -= TickInterval;
            TickOnce();
        }
    }

    private void TickOnce()
    {
        if (!EnsureHandlersBuilt()) return;
        foreach (var dim in WorldManager.Instance.Dimensions.Values)
        {
            var rng = GetOrCreateRng(dim);
            foreach (var chunk in dim.GetEnableChunks())
            {
                for (int sectionIdx = chunk.MinSubChunkIndex; sectionIdx <= chunk.MaxSubChunkIndex; sectionIdx++)
                {
                    var sub = chunk.GetSubChunk(sectionIdx);
                    if (sub == null) continue;   // never received blocks: all air
                    for (int s = 0; s < RandomTickSpeedPerSection; s++)
                    {
                        int idx = rng.Next(SectionVolume);
                        int x = idx >> 8;        // blockData layout: x*256 + y*16 + z
                        int y = (idx >> 4) & 15;
                        int z = idx & 15;
                        ushort stateId = sub.GetBlockAtRaw(x, y, z);
                        if (stateId == 0) continue;            // air
                        var handler = handlerByState[stateId];
                        if (handler == null) continue;         // block declared no random tick
                        var ctx = new RandomTickContext
                        {
                            Dim = dim,
                            Pos = Chunk.ChunkLocalCoordToDimensionCoord(chunk.ChunkCoord,
                                new Vector3Int(x, sectionIdx * 16 + y, z)),
                            StateId = stateId,
                            Random = rng
                        };
                        handler(ctx);
                    }
                }
            }
        }
    }

    private bool EnsureHandlersBuilt()
    {
        int count = ResourceSystem.Instance.BlockStates.Count;
        if (count == 0) return false;   // BlockStates not materialized yet (PreFreeze boot phase)
        if (handlerByState.Length == count) return true;
        handlerByState = new System.Action<RandomTickContext>[count];
        for (ushort stateId = 0; stateId < count; stateId++)
        {
            if (ResourceSystem.Instance.BlockStates.TryGetResourceWithNumberId(stateId, out var state))
                handlerByState[stateId] = state.Block.RandomTick;
        }
        return true;
    }

    private System.Random GetOrCreateRng(Dimension dim)
    {
        if (rngByDimension.TryGetValue(dim, out var rng)) return rng;
        if (!ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(dim.DimensionDefinitionInfo.FullName, out var dimId))
            dimId = 0;
        rng = new System.Random(unchecked(WorldManager.Instance.Seed * 397 ^ dimId * 7919));
        rngByDimension.Add(dim, rng);
        return rng;
    }
}

// Passed to a block's RandomTick handler. The context is the only sanctioned
// way for random-tick logic to touch the world (design doc §5).
public struct RandomTickContext
{
    public Dimension Dim;
    public Vector3Int Pos;        // dimension coord of the ticked block
    public ushort StateId;        // current global state id of the ticked block
    public System.Random Random;  // shared dimension RNG (vanilla level.random alike)

    // Reads with Dim.GetBlockAt semantics: an unloaded/disabled chunk reads as
    // 0 (air). Never generates or loads a chunk.
    public ushort GetStateId(Vector3Int pos) => Dim.GetBlockAt(pos);
    public bool IsAir(Vector3Int pos) => GetStateId(pos) == 0;

    // Write gate (rules R4): into an enabled chunk only - no chunk generation,
    // replace-style Chunk.ForceSetBlockAt path (BlockChangedEvent + save dirty
    // flag; TrySetBlockAt refuses occupied cells, and a conversion target is
    // always occupied). Returns false when the target sits in an unloaded
    // chunk or the write failed; the caller silently accepts that
    // (probabilistic logic).
    public bool TrySetBlockState(Vector3Int pos, ushort newStateId)
    {
        var chunkCoord = Dimension.DimensionCoordToChunkCoord(pos);
        if (!Dim.IsChunkEnabled(chunkCoord) || !Dim.TryGetChunk(chunkCoord, out var chunk)) return false;
        return chunk.ForceSetBlockAt(Chunk.DimensionCoordToChunkLocalCoord(pos), newStateId);
    }
}
