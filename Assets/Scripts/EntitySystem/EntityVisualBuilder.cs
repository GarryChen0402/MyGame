using System.Collections.Generic;
using UnityEngine;

// Builds the GameObject hierarchy of an EntityModel: one GO per CustomCube,
// parented per Hierarchy, each carrying its own cube mesh. The GO sits at the
// cube's position (the rotation anchor in parent space); the mesh is offset by
// the cube's pivot so the cube spans [position - pivot, position - pivot + size]
// in parent space when not rotated.
// Face textures are resolved through faceTextureIds (face name -> texture
// FullName) onto the atlas; pass null to use a full-texture rect.
public static class EntityVisualBuilder
{
    // Builds the visual of an EntityModel whatever its source kind: Json
    // sources go through the cube-tree builder (parsing lazily first), Prefab
    // sources are instantiated directly. Returns null when the source is
    // missing or unbuildable (callers must tolerate null).
    public static EntityVisual Build(EntityModel model, Dictionary<string, string> faceTextureIds,
                                     Material material, Transform parent)
    {
        if(model == null)return null;
        if(model.SourceType == EntityModelSourceType.Prefab)
            return BuildPrefab(model, parent);
        if(!model.EnsureParsed())
        {
            Debug.LogError($"[EntityVisualBuilder] cannot build json model '{model.FullName}' from '{model.SourcePath}'");
            return null;
        }
        return BuildFromRoots(model, model.Roots, faceTextureIds, material, parent);
    }

    // Builds the hierarchy subtree rooted at `rootId` (that cube and its
    // descendants) instead of the model's own roots - the model editor shows
    // one part per page this way. The subtree root keeps the cube's position,
    // so it sits exactly where it does inside the full model. Cube-only: a
    // Prefab source has no cube tree to pick a subtree from.
    public static EntityVisual BuildFrom(EntityModel model, string rootId,
                                         Dictionary<string, string> faceTextureIds,
                                         Material material, Transform parent)
    {
        if(model == null)return null;
        if(model.SourceType == EntityModelSourceType.Prefab)
        {
            Debug.LogWarning($"[EntityVisualBuilder] '{model.FullName}' is a Prefab source; subtree building applies to Json cube models only");
            return null;
        }
        if(!model.EnsureParsed())return null;
        return BuildFromRoots(model, new List<string> { rootId }, faceTextureIds, material, parent);
    }

    // Prefab source: instantiate the Resources prefab as the visual root and
    // index its named child transforms (bones/cubes) so part-driven consumers
    // (EntityAnimator, editor picking) can address prefab parts by name too.
    // The prefab carries its own meshes/materials/Animator; no cube data exists.
    private static EntityVisual BuildPrefab(EntityModel model, Transform parent)
    {
        var prefab = Resources.Load<GameObject>(model.SourcePath);
        if(prefab == null)
        {
            Debug.LogError($"[EntityVisualBuilder] prefab source '{model.SourcePath}' of '{model.FullName}' not found (Resources)");
            return null;
        }
        var go = Object.Instantiate(prefab, parent, false);
        var visual = new EntityVisual { Root = go.transform, PartTransforms = new(), BasePositions = new() };
        foreach(var part in go.GetComponentsInChildren<Transform>(true))
        {
            if(part == go.transform)continue;
            if(visual.PartTransforms.ContainsKey(part.name))continue;   // first (outermost) hit wins
            visual.PartTransforms[part.name] = part;
            visual.BasePositions[part.name] = part.localPosition;
        }
        return visual;
    }

    private static EntityVisual BuildFromRoots(EntityModel model, List<string> roots,
                                               Dictionary<string, string> faceTextureIds,
                                               Material material, Transform parent)
    {
        if(material == null)material = ResourceSystem.Instance.BlockMaterial;

        var root = new GameObject(roots.Count == 1 ? roots[0] : model.FullName);
        root.transform.SetParent(parent, false);

        var faceRects = new Dictionary<string, Rect>();
        if(faceTextureIds != null)
            foreach(var kv in faceTextureIds)
                if(ResourceSystem.Instance.Textures.TryGetResourceWithFullName(kv.Value, out var tex))
                    faceRects[kv.Key] = tex.AtlasUVRect;

        var visual = new EntityVisual { Root = root.transform, PartTransforms = new(), BasePositions = new() };
        foreach(string id in roots)
            BuildCube(model, id, root.transform, faceRects, material, visual);
        return visual;
    }

    private static void BuildCube(EntityModel model, string stringId, Transform parent,
                                  Dictionary<string, Rect> faceRects, Material material, EntityVisual visual)
    {
        var go = new GameObject(stringId);
        go.transform.SetParent(parent, false);

        // A hierarchy node without a cube entry is an empty group GO (no mesh),
        // used purely to organize children.
        if(model.Cubes.TryGetValue(stringId, out var cube))
        {
            go.transform.localPosition = cube.Position;   // the GO is the rotation anchor in parent space

            Vector3 meshOrigin = -cube.Pivot;   // anchor offset inside the cube
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            foreach(var face in cube.Faces)
                ExtendFace(face.Key, face.Value, meshOrigin, faceRects, verts, uvs, colors, normals, tris);

            if(verts.Count > 0)
            {
                var mesh = new Mesh();
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colors);
                mesh.SetNormals(normals);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }

            visual.BasePositions[stringId] = cube.Position;
        }
        else
        {
            visual.BasePositions[stringId] = Vector3.zero;
        }

        visual.PartTransforms[stringId] = go.transform;

        if(model.Hierarchy.TryGetValue(stringId, out var children))
            foreach(string child in children)
                BuildCube(model, child, go.transform, faceRects, material, visual);
    }

    private static void ExtendFace(string faceName, ModelFaceData face, Vector3 origin,
                                   Dictionary<string, Rect> faceRects,
                                   List<Vector3> verts, List<Vector2> uvs, List<Color> colors,
                                   List<Vector3> normals, List<int> tris)
    {
        Rect rect = faceRects != null && faceRects.TryGetValue(faceName, out Rect r) ? r : new Rect(0, 0, 1, 1);
        int vStart = verts.Count;
        foreach(var v in face.verts) verts.Add(v + origin);
        foreach(var u in face.uv) uvs.Add(new Vector2(u.x * rect.width + rect.x, u.y * rect.height + rect.y));
        colors.AddRange(face.colors);
        normals.AddRange(face.normals);
        foreach(var t in face.triangles) tris.Add(t + vStart);
    }
}

// The built hierarchy of an entity model: the model root, a lookup from
// StringId to the cube's GO, and each part's initial localPosition (animation
// offsets are applied on top of it).
public class EntityVisual
{
    public Transform Root;
    public Dictionary<string, Transform> PartTransforms;
    public Dictionary<string, Vector3> BasePositions;
}
