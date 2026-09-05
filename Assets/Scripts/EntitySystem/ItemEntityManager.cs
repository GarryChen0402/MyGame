using System.Collections.Generic;
using UnityEngine;

// Keeps the live reference set of item drops and re-parents drops that cross
// chunk borders once their move settles. Ticking itself lives in
// EntityManager.Update (the Entity ctor auto-registers every ItemEntity, so
// drops tick exactly once per frame from there - v1 also ticked them from
// here, which integrated the physics twice per frame). Chunks own the data
// (Chunk.ItemEntities), see design doc 掉落物ItemEntity实现方案.md.
public class ItemEntityManager
{
    public static ItemEntityManager Instance { get; } = new();

    private readonly HashSet<ItemEntity> tracked = new();
    private readonly List<ItemEntity> tickBuffer = new();   // snapshot for iteration

    // Lazy singleton: first Instance access happens from WorldManager.Tick
    // (GameLoopDriver), by which point EventBus/PhysicsManager subscriptions are
    // ready (the same lazy pattern as BlockEntityManager).
    private ItemEntityManager()
    {
        EventBus.Instance.Subscribe<ChunkUnloadedEvent>(OnChunkUnloaded);
    }

    public void Register(ItemEntity entity)
    {
        if(entity == null)return;
        if(!tracked.Add(entity))
            Debug.LogWarning("[ItemEntityManager] item entity already tracked");
    }

    public void Unregister(ItemEntity entity)
    {
        if(entity != null)tracked.Remove(entity);
    }

    // Frame driver, mounted in WorldManager.Tick after EntityManager.Update:
    // the frame's movement has already settled, so chunk ownership can follow
    // the drop's new position.
    public void Update()
    {
        if(tracked.Count == 0)return;
        tickBuffer.Clear();
        tickBuffer.AddRange(tracked);   // snapshot: despawn mid-update can't break iteration
        foreach(var entity in tickBuffer)
            if(tracked.Contains(entity))VerifyOwnership(entity);
    }

    // After movement settles, the owner chunk must follow the entity position
    // (vanilla: an entity belongs to exactly one chunk and migrates live).
    // v1: flying into an unloaded chunk despawns the drop (design doc §5.1).
    private void VerifyOwnership(ItemEntity entity)
    {
        if(entity.OwnerChunk == null)return;
        if(!WorldManager.Instance.TryGetDimension(entity.DimensionId, out var dim))
        {
            DespawnItemEntity(entity);   // dimension gone (v1: no cross-dim entity travel)
            return;
        }
        Vector2Int coord = Dimension.WorldPosToChunkCoord(entity.Position);
        if(coord == entity.OwnerChunk.ChunkCoord)return;
        if(dim.TryGetChunk(coord, out var target))
        {
            entity.OwnerChunk.RemoveItemEntity(entity);   // old chunk drops the reference
            target.RegisterItemEntity(entity);            // new chunk takes over ownership
        }
        else
        {
            DespawnItemEntity(entity);   // out of the loaded area (v1)
        }
    }

    // Total spawn entry: build data -> place into the owner chunk -> tracked
    // -> render shell GO (EntityRenderManager reads the data one-way).
    public void SpawnItemEntity(ushort dimensionId, Vector3 position, ItemStack stack,
        Vector3? motion = null, float pickupDelay = 0.5f)
    {
        if(stack == null || stack.IsEmpty())return;
        if(!WorldManager.Instance.TryGetDimension(dimensionId, out var dim))return;
        if(!dim.TryGetChunk(Dimension.WorldPosToChunkCoord(position), out var chunk))return;   // v1: spawns only happen in loaded chunks

        var entity = new ItemEntity();   // ctor publishes SummonEntity -> PhysicsManager registers the AABB
        entity.DimensionId = dimensionId;
        entity.Stack = stack;
        entity.Motion = motion ?? Vector3.zero;
        entity.PickupDelay = pickupDelay;
        entity.SetPosition(position);
        chunk.RegisterItemEntity(entity);   // into Chunk.ItemEntities + tracked
        EntityRenderManager.Instance.Attach(entity);
    }

    // Total despawn: idempotent (tracked membership is the sentinel). Removes
    // the entity from its chunk's list, drops the live reference, destroys the
    // render shell and publishes DestroyEntity so PhysicsManager forgets the
    // AABB (leak guard).
    public void DespawnItemEntity(ItemEntity entity)
    {
        if(entity == null || !tracked.Remove(entity))return;
        EntityManager.Instance.Unregister(entity);   // leave the tick set or EntityManager.Update keeps ticking the corpse
        if(entity.OwnerChunk != null)
        {
            entity.OwnerChunk.ItemEntities.Remove(entity);
            entity.OwnerChunk = null;
        }
        entity.OnDestroy();   // DestroyEntity -> PhysicsManager.UnRegister, shell destroyed by EntityRenderManager
    }

    // Chunk disabled/unloaded: its drops stop ticking and are dropped with it
    // (v1: not persisted - design doc §8; v2 snapshots here instead).
    private void OnChunkUnloaded(ChunkUnloadedEvent evt) => evt.Chunk.UnregisterAllItemEntities();
}
