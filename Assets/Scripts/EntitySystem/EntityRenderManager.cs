using System.Collections.Generic;
using UnityEngine;

// Owns the render shell of every live dynamic entity - one GameObject per
// entity, keyed by EntityId (rule R-C2-2) so a despawn can destroy the GO.
// Shell assembly is event-driven: the logic spawn entries publish
// EntityShellSpawnEvent once the entity's data is complete (the former Attach
// direct calls are gone - rule R-C2-0); EntityShellDespawnEvent, published
// from Entity.OnDestroy, removes the GO. Shells only read entity mirrors
// one-way each frame.
public class EntityRenderManager
{
    public static EntityRenderManager Instance { get; } = new();

    private readonly Dictionary<int, GameObject> shells = new();
    private Transform dynamicRoot;

    private EntityRenderManager()
    {
        EventBus.Instance.Subscribe<EntityShellSpawnEvent>(OnShellSpawn);
        EventBus.Instance.Subscribe<EntityShellDespawnEvent>(OnShellDespawn);
    }

    // No-op for unknown kinds (Player keeps its own renderer), missing data
    // and already-shelled entities (idempotent). Assembly data all comes from
    // the DTO - no entity field is read here.
    private void OnShellSpawn(EntityShellSpawnEvent evt)
    {
        if(evt.mirror == null || shells.ContainsKey(evt.entityId))return;
        if(evt.isItem)AttachItem(evt);
        else AttachMob(evt);
    }

    private void AttachItem(EntityShellSpawnEvent evt)
    {
        if(evt.itemId == 0)return;
        var go = new GameObject($"Item Drop {evt.itemId}");
        go.transform.SetParent(DynamicRoot, false);
        // Meshes span 1m; the 0.25 scale matches the entity's physics box.
        go.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
        go.AddComponent<EntityRenderer>().Bind(evt.mirror, evt.itemId);
        shells[evt.entityId] = go;
    }

    private void AttachMob(EntityShellSpawnEvent evt)
    {
        if(string.IsNullOrEmpty(evt.modelId))return;
        if(!ResourceSystem.Instance.EntityModels.TryGetResourceWithFullName(evt.modelId, out var model))return;
        var visual = EntityVisualBuilder.Build(model, null, null, DynamicRoot);
        if(visual?.Root == null)return;   // missing/unbuildable source already logged by the builder
        visual.Root.gameObject.AddComponent<MobVisualSync>().Bind(evt.mirror, visual);
        shells[evt.entityId] = visual.Root.gameObject;
    }

    private void OnShellDespawn(EntityShellDespawnEvent evt)
    {
        if(shells.Remove(evt.entityId, out var go))Object.Destroy(go);
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
