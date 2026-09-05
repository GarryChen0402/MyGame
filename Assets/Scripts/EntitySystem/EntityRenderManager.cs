using System.Collections.Generic;
using UnityEngine;

// Owns the render shell of every live dynamic entity - one GameObject per
// entity, keyed here so a despawn can destroy the GO. Data/logic stay in the
// entity classes; shells only read entity data one-way each frame.
// The table is filled by Attach, which the spawn entry points call once the
// entity's data is complete (SummonEntityEvent fires too early - in the Entity
// ctor - before Stack/Definition exist, so it carries no buildable info), and
// emptied event-driven by DestroyEntity: the removal flow publishes it through
// entity.OnDestroy(), so shells that are not managed here are simply ignored.
public class EntityRenderManager
{
    public static EntityRenderManager Instance { get; } = new();

    private readonly Dictionary<Entity, GameObject> shells = new();
    private Transform dynamicRoot;

    private EntityRenderManager()
    {
        EventBus.Instance.Subscribe<DestroyEntity>(OnDestroyEntity);
    }

    // No-op for unknown entity kinds (Player keeps its own renderer), for
    // data-less entities and for ones already attached (idempotent).
    public void Attach(Entity entity)
    {
        if(entity == null || shells.ContainsKey(entity))return;
        if(entity is ItemEntity item)AttachItem(item);
        else if(entity is MobEntity mob)AttachMob(mob);
    }

    private void AttachItem(ItemEntity entity)
    {
        if(entity.Stack == null)return;
        var go = new GameObject($"Item Drop {entity.Stack.itemId}");
        go.transform.SetParent(DynamicRoot, false);
        // Meshes span 1m; the 0.25 scale matches the entity's physics box.
        go.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        go.AddComponent<EntityRenderer>().Bind(entity);
        shells[entity] = go;
    }

    private void AttachMob(MobEntity entity)
    {
        if(entity.Definition == null || string.IsNullOrEmpty(entity.Definition.ModelId))return;
        if(!ResourceSystem.Instance.EntityModels.TryGetResourceWithFullName(entity.Definition.ModelId, out var model))return;
        var visual = EntityVisualBuilder.Build(model, null, null, DynamicRoot);
        if(visual?.Root == null)return;   // missing/unbuildable source already logged by the builder
        visual.Root.gameObject.AddComponent<MobVisualSync>().Bind(entity);
        shells[entity] = visual.Root.gameObject;
    }

    private void OnDestroyEntity(DestroyEntity evt)
    {
        if(shells.Remove(evt.entity, out var go))Object.Destroy(go);
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
}
