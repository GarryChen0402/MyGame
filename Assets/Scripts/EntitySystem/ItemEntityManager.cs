using System.Collections.Generic;
using UnityEngine;

// Drives every live item drop once per rendered frame. Mirrors
// BlockEntityManager's shape: chunks own the data (Chunk.ItemEntities), this
// manager keeps a reference set of living drops only and ticks them. Unlike
// BlockEntityManager there is no 20Hz accumulator - item physics is integrated
// per rendered frame with deltaTime (design doc §5).
public class ItemEntityManager
{
    public static ItemEntityManager Instance { get; } = new();

    private readonly HashSet<ItemEntity> tracked = new();
    private readonly List<ItemEntity> tickBuffer = new();   // snapshot for iteration
    // Render shells (one GO per entity, EntityRenderer only). Keyed here so a
    // despawn can destroy the GO; migration between chunks keeps the shell.
    private readonly Dictionary<ItemEntity, GameObject> shells = new();
    private Transform dynamicRoot;

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

    // Frame driver, mounted in WorldManager.Tick (GameLoopDriver) next to
    // BlockEntityManager.Tick.
    public void Update(float deltaTime)
    {
        if(tracked.Count == 0)return;
        tickBuffer.Clear();
        tickBuffer.AddRange(tracked);   // snapshot: despawn mid-update can't break iteration
        foreach(var entity in tickBuffer)
            if(tracked.Contains(entity))entity.OnUpdate(deltaTime);
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
    // -> render shell GO (EntityRenderer reads the data one-way).
    public void SpawnItemEntity(ushort dimensionId, Vector3 position, ItemStack stack,
        Vector3? motion = null, float pickupDelay = 0.5f)
    {
        if(stack == null || stack.IsEmpty())return;
        if(!WorldManager.Instance.TryGetDimension(dimensionId, out var dim))return;
        if(!dim.TryGetChunk(Dimension.WorldPosToChunkCoord(position), out var chunk))return;   // v1: spawns only happen in loaded chunks

        var entity = new ItemEntity();   // ctor publishes SummonEntity -> PhysicsManager registers the AABB
        entity.DimensionId = dimensionId;
        entity.Stack = stack;
        entity.MotionSpeed = motion ?? Vector3.zero;
        entity.PickupDelay = pickupDelay;
        entity.SetPosition(position);
        chunk.RegisterItemEntity(entity);   // into Chunk.ItemEntities + tracked
        CreateRenderShell(entity);
    }

    // Total despawn: idempotent (tracked membership is the sentinel). Removes
    // the entity from its chunk's list, drops the live reference, destroys the
    // render shell and publishes DestroyEntity so PhysicsManager forgets the
    // AABB (leak guard).
    public void DespawnItemEntity(ItemEntity entity)
    {
        if(entity == null || !tracked.Remove(entity))return;
        if(entity.OwnerChunk != null)
        {
            entity.OwnerChunk.ItemEntities.Remove(entity);
            entity.OwnerChunk = null;
        }
        if(shells.Remove(entity, out var go))Object.Destroy(go);
        entity.OnDestroy();   // DestroyEntity -> PhysicsManager.UnRegister
    }

    // All shells live under one lazy "DynamicEntities" root so they can be
    // parented/filtered together; the root follows the world renderer.
    private Transform DynamicRoot
    {
        get
        {
            if(dynamicRoot == null)
            {
                var go = new GameObject("DynamicEntities");
                if(WorldRenderer.Instance != null)
                    go.transform.SetParent(WorldRenderer.Instance.transform, false);
                dynamicRoot = go.transform;
            }
            return dynamicRoot;
        }
    }

    private void CreateRenderShell(ItemEntity entity)
    {
        var go = new GameObject($"Item Drop {entity.Stack.itemId}");
        go.transform.SetParent(DynamicRoot, false);
        // Meshes span 1m; the 0.25 scale matches the entity's physics box.
        go.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        var shell = go.AddComponent<EntityRenderer>();
        shell.Bind(entity);
        shells[entity] = go;
    }

    // Chunk disabled/unloaded: its drops stop ticking and are dropped with it
    // (v1: not persisted - design doc §8; v2 snapshots here instead).
    private void OnChunkUnloaded(ChunkUnloadedEvent evt) => evt.Chunk.UnregisterAllItemEntities();
}
