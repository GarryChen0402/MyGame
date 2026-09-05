using System.Collections.Generic;
using UnityEngine;

// Model-editor data utilities (design doc §4.1). P1 scope is the copy and
// pixel <-> uvRect conversion layer only; the loaded-model / face-texture-set
// session state and the UI commit path arrive in later phases. The editor
// edits a session copy and never the registry resource: unexported changes
// would otherwise leak into every other consumer of the model (player
// preview, other UIs).
public static class EntityModelEditorSession
{
    public static EntityModel DeepCopy(EntityModel src)
    {
        src.EnsureParsed();   // registry descriptors parse lazily; the copy needs the cube tree
        var copy = new EntityModel
        {
            modId = src.modId,
            name = src.name,
            SourceType = src.SourceType,
            SourcePath = src.SourcePath,
            Roots = new List<string>(src.Roots),
            Cubes = new Dictionary<string, CustomCube>(),
            Hierarchy = new Dictionary<string, List<string>>()
        };
        foreach(var kv in src.Cubes)
            copy.Cubes[kv.Key] = DeepCopyCube(kv.Value);
        foreach(var kv in src.Hierarchy)
            copy.Hierarchy[kv.Key] = new List<string>(kv.Value);
        return copy;
    }

    private static CustomCube DeepCopyCube(CustomCube src)
    {
        var copy = new CustomCube
        {
            StringId = src.StringId,
            Size = src.Size,
            Pivot = src.Pivot,
            Position = src.Position,
            Faces = new Dictionary<string, ModelFaceData>()
        };
        foreach(var kv in src.Faces)
            copy.Faces[kv.Key] = DeepCopyFace(kv.Value);
        return copy;
    }

    // ModelFaceData is a struct of List references: copy each list so an edit
    // on the session side cannot reach the registry side.
    private static ModelFaceData DeepCopyFace(ModelFaceData src) => new()
    {
        verts = new List<Vector3>(src.verts),
        uv = new List<Vector2>(src.uv),
        colors = new List<Color>(src.colors),
        normals = new List<Vector3>(src.normals),
        triangles = new List<int>(src.triangles),
        canBeOccluded = src.canBeOccluded
    };

    // ---- uvRect <-> baked UVs ----

    // Reconstructs the normalized uvRect a baked face's 4 corner UVs came from.
    // The canonical corner order pins the corners to the rect corners, so this
    // is exactly min/max and loses nothing (the format's rect invariant).
    public static Vector4 UvRectFromFaceUvs(List<Vector2> uv)
    {
        float minU = float.MaxValue, minV = float.MaxValue;
        float maxU = float.MinValue, maxV = float.MinValue;
        foreach(var p in uv)
        {
            minU = Mathf.Min(minU, p.x); minV = Mathf.Min(minV, p.y);
            maxU = Mathf.Max(maxU, p.x); maxV = Mathf.Max(maxV, p.y);
        }
        return new Vector4(minU, minV, maxU - minU, maxV - minV);
    }

    // ---- pixel <-> normalized rect conversion ----

    // Integer pixel rect -> normalized uvRect. Origin is the bottom-left of
    // the face texture, matching the engine's v-up convention (v measured from
    // the texture bottom). texW/texH are the texture's original pixel size
    // (TextureResource.Atlas.width/height), which the json's rect is relative to.
    public static Vector4 PixelsToUvRect(RectInt pixels, int texW, int texH) => new(
        pixels.x / (float)texW,
        pixels.y / (float)texH,
        pixels.width / (float)texW,
        pixels.height / (float)texH);

    // Normalized uvRect -> integer pixel rect (rounds to the nearest pixel;
    // clamped so the rect never escapes the texture).
    public static RectInt UvRectToPixels(Vector4 uv, int texW, int texH)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(uv.x * texW), 0, texW);
        int y = Mathf.Clamp(Mathf.RoundToInt(uv.y * texH), 0, texH);
        int w = Mathf.Clamp(Mathf.RoundToInt(uv.z * texW), 0, texW - x);
        int h = Mathf.Clamp(Mathf.RoundToInt(uv.w * texH), 0, texH - y);
        return new RectInt(x, y, w, h);
    }

    // Snaps a normalized rect onto the texture's 1px grid (round-trip
    // stability for non-POT textures / hand-written uvRects, design doc §6.1:
    // the pixel intent survives re-export). Values already on the grid -
    // everything PixelsToUvRect produces - pass through unchanged.
    public static Vector4 QuantizeToPixelGrid(Vector4 uv, int texW, int texH)
        => PixelsToUvRect(UvRectToPixels(uv, texW, texH), texW, texH);
}
