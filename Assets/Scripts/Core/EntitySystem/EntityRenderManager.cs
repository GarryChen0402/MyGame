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
        // Json cube shells render on the mob hurt-flash material (atlas under
        // Entity/HurtFlash); Prefab shells get their own materials swapped to
        // flash twins below - either way the shell answers _FlashAmount.
        var visual = EntityVisualBuilder.Build(model, null,
            ResourceSystem.Instance.MobFlashMaterial, DynamicRoot);
        if(visual?.Root == null)return;   // missing/unbuildable source already logged by the builder
        if(model.SourceType == EntityModelSourceType.Prefab)SwapToHurtFlash(visual.Root);
        visual.Root.gameObject.AddComponent<MobVisualSync>().Bind(evt.mirror, visual);
        shells[evt.entityId] = visual.Root.gameObject;
    }

    // Prefab assets carry their own materials, which have no flash input; each
    // is swapped for a twin under Entity/HurtFlash so the shell can flash red
    // on hurt. Twins are cached by source material instance (every spawn of
    // the same prefab reads the same asset material), so clones stay shared
    // across entities - batching survives, nothing is allocated per shell.
    private static readonly Dictionary<Material, Material> hurtFlashTwins = new();

    private static void SwapToHurtFlash(Transform visualRoot)
    {
        Shader flashShader = Resources.Load<Shader>("Shaders/EntityHurtFlash");
        if(flashShader == null)return;   // shader asset not imported yet: keep source materials
        foreach(var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
        {
            Material source = renderer.sharedMaterial;
            if(source == null || source.shader == flashShader)continue;
            if(!hurtFlashTwins.TryGetValue(source, out var twin))
            {
                twin = new Material(source) { shader = flashShader };
                // glTFast PBR assets store the albedo under baseColorTexture
                // (Shader Graphs/glTF-pbrMetallicRoughness), not URP Lit's
                // _BaseMap; a shader swap drops names the new shader lacks, so
                // re-point albedo-like source textures onto _BaseMap. Same-name
                // slots (_BaseMap) survive the swap untouched.
                foreach(string name in source.GetTexturePropertyNames())
                {
                    Texture tex = source.GetTexture(name);
                    if(tex == null || twin.HasProperty(name))continue;
                    if(name == "_MainTex" || name.ToLowerInvariant().Contains("basecolor"))
                        twin.SetTexture("_BaseMap", tex);
                }
                // Cutout sources (glTF hair/hat layers with _ALPHATEST_ON and a
                // TransparentCutout queue) keep their clip state and sorting or
                // their transparent pixels would render as opaque black. _Cull
                // is a same-name property, so double-sided sources keep it.
                if(source.IsKeywordEnabled("_ALPHATEST_ON"))twin.EnableKeyword("_ALPHATEST_ON");
                twin.renderQueue = source.renderQueue;
                hurtFlashTwins[source] = twin;
            }
            renderer.sharedMaterial = twin;
        }
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
