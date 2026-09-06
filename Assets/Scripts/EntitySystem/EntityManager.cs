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
        EntityRenderManager.Instance.Attach(target);   // shell, once the data is ready
        return true;
    }
}