using System.Collections.Generic;
using UnityEngine;

// Serializes a runtime EntityModel back into the json format EntityModelParser
// consumes (closure: an exported file re-imports losslessly on next boot).
// Sizes are scaled back up to pixels (1m = 16px); face uvRects are re-derived
// from the baked 4-corner UVs, which the canonical corner order pins exactly
// to the rect corners, so no rect state is lost during baking.
public static class EntityModelSerializer
{
    public static string ToJson(EntityModel model)
    {
        var data = new EntityModelData
        {
            modId = model.modId,
            name = model.name,
            roots = ToArray(model.Roots),
            cubes = ToCubes(model),
            hierarchy = ToHierarchy(model)
        };
        return JsonUtility.ToJson(data, true);
    }

    private static CubeEntry[] ToCubes(EntityModel model)
    {
        var list = new List<CubeEntry>();
        foreach(var cube in model.Cubes.Values)
            list.Add(ToCubeEntry(cube));
        return list.ToArray();
    }

    private static CubeEntry ToCubeEntry(CustomCube cube)
    {
        return new CubeEntry
        {
            stringId = cube.StringId,
            size = ToPixels(cube.Size),
            pivot = ToPixels(cube.Pivot),
            position = ToPixels(cube.Position),
            top = ToFace(cube, "top"),
            bottom = ToFace(cube, "bottom"),
            front = ToFace(cube, "front"),
            back = ToFace(cube, "back"),
            left = ToFace(cube, "left"),
            right = ToFace(cube, "right")
        };
    }

    private static FaceEntry ToFace(CustomCube cube, string key)
    {
        if(!cube.Faces.TryGetValue(key, out var face))return null;
        return new FaceEntry
        {
            uvRect = EntityModelEditorSession.UvRectFromFaceUvs(face.uv),
            // The bake side maps an unset json color (0,0,0,0) to white, so
            // exporting the baked color keeps the round trip visually identical.
            color = face.colors.Count > 0 ? face.colors[0] : Color.white,
            canBeOccluded = face.canBeOccluded
        };
    }

    private static HierarchyEntry[] ToHierarchy(EntityModel model)
    {
        var list = new List<HierarchyEntry>();
        foreach(var kv in model.Hierarchy)
            list.Add(new HierarchyEntry { parent = kv.Key, children = ToArray(kv.Value) });
        return list.ToArray();
    }

    // 16 is a power of two, so any pixel value the parser divided by is binary
    // exact: multiply-and-round restores the original integer losslessly.
    private static Vector3 ToPixels(Vector3 meters) => new(
        Mathf.RoundToInt(meters.x * EntityModelParser.PixelsPerBlock),
        Mathf.RoundToInt(meters.y * EntityModelParser.PixelsPerBlock),
        Mathf.RoundToInt(meters.z * EntityModelParser.PixelsPerBlock));

    private static string[] ToArray(List<string> list) => list?.ToArray() ?? new string[0];
}
