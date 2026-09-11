using System.Collections.Generic;
using UnityEngine;

public class EntityManager
{
    private static EntityManager instance = new();
    public static EntityManager Instance => instance;
    public bool InUpdating {get; private set;} = false;
    public readonly HashSet<Entity> entities = new();
    public readonly List<Entity> pendingRemoveList = new();
    public readonly List<Entity> pendingAddList = new();

    private EntityManager()
    {
        EventBus.Instance.Subscribe<SummonEntityEvent>((evt)=> Register(evt.entity));
    }

    public void Register(Entity entity)
    {
        if(entity == null)return;
        if(!InUpdating)entities.Add(entity);
        else pendingAddList.Add(entity);
    }

    public void Unregister(Entity entity)
    {
        if(entity == null)return;
        if(!InUpdating)entities.Remove(entity);
        else pendingRemoveList.Add(entity);
    }

    public void Update(float dt)
    {
        // Tick boundary: snapshot every entity's prev state before any of them
        // moves, so render interpolation always lerps within one tick's span
        // (design doc 固定Tick时钟与渲染插值改造-代码设计.md §3). The snapshot
        // loop touches no events, so the set cannot mutate mid-pass.
        foreach(var entity in entities)entity.SnapshotTickStart();
        InUpdating = true;
        foreach(var entity in entities)entity.OnUpdate(dt);
        InUpdating = false;

        // Unified entity-entity push pass: runs after every entity finished its
        // own move + block collision this frame. Pending spawns are flushed
        // after it - a just-spawned entity has not moved yet and sits the pass
        // out (design doc 生物实体碰撞-代码设计 §3).
        EntityPushResolver.RunPass(entities);

        foreach(var e in pendingAddList)Register(e);
        foreach(var e in pendingRemoveList)Unregister(e);
    }

    public static bool SummonMobEntity(Vector3 targetPos, string MobDefinitionId)
    {
        if (!ResourceSystem.Instance.MobDefinitions.TryGetResourceWithFullName(MobDefinitionId, out var def))
        {
            Debug.LogWarning($"[EntityManager] unknown mob definition '{MobDefinitionId}'");
            return false;
        }
        var target = new MobEntity();
        target.Init(def, targetPos);
        // Shell data channel (rule R-C2-3): register the entity mirror and
        // publish the spawn event now that the data is complete - replaces the
        // former EntityRenderManager.Attach direct call.
        var mirror = MirrorSync.Instance.RegisterEntityMirror(target);
        EventBus.Instance.Publish(new EntityShellSpawnEvent
        {
            entityId = target.EntityId,
            isItem = false,
            modelId = def.ModelId,
            mirror = mirror
        });
        return true;
    }

    // Session teardown sweep (WorldSession.ExitWorld). The Player is resident
    // across sessions (its ctor auto-registered it and it must survive) -
    // skipped. Drops usually already left through the chunk-unload cascade;
    // DespawnItemEntity is idempotent (tracked-membership sentinel), so
    // leftovers go through the same door.
    public void DestroyAll()
    {
        var snapshot = new List<Entity>(entities);   // Unregister mutates the set
        foreach(var entity in snapshot)
        {
            if(entity is Player)continue;
            if(entity is ItemEntity item)
            {
                ItemEntityManager.Instance.DespawnItemEntity(item);
                continue;
            }
            Unregister(entity);
            entity.OnDestroy();   // DestroyEntity (PhysicsManager) + mirror + shell despawn
        }
    }
}