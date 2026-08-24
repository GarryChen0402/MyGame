using System.Collections.Generic;
using UnityEngine;

public class CustomModel : ResourceType
{
    // public string ModelName;
    public Dictionary<string, ModelFaceData> MeshData;

    private Dictionary<string, Vector3Int> faceDirectionsCache;

    // Returns face name -> direction of the outward normal, rounded to integer axes.
    // Cached lazily because MeshData never changes after parsing.
    public Dictionary<string, Vector3Int> GetFaceDirections()
    {
        if (faceDirectionsCache == null)
        {
            faceDirectionsCache = new Dictionary<string, Vector3Int>();
            foreach (var kvp in MeshData)
            {
                Vector3 n = kvp.Value.normals.Count > 0 ? kvp.Value.normals[0] : Vector3.zero;
                faceDirectionsCache[kvp.Key] = new Vector3Int(
                    Mathf.RoundToInt(n.x), Mathf.RoundToInt(n.y), Mathf.RoundToInt(n.z));
            }
        }
        return faceDirectionsCache;
    }

    // Appends vertex data of all non-occluded faces to the given lists.
    // occlusionMask: face name -> whether a neighbor blocks that direction; faces occluded and
    //   canBeOccluded are skipped. faceRects: face name -> atlas region for UV mapping.
    public void ExtendModelMesh(Vector3 origin, List<Vector3> verts, List<Vector2> uv, List<Color> colors,
        List<Vector3> normals, List<int> triangles, Dictionary<string, bool> occlusionMask = null,
        Dictionary<string, Rect> faceRects = null)
    {
        foreach (var kvp in MeshData)
        {
            if (occlusionMask != null && occlusionMask.TryGetValue(kvp.Key, out bool occluded) && occluded && kvp.Value.canBeOccluded)
                continue;

            Rect rect = faceRects != null && faceRects.TryGetValue(kvp.Key, out Rect r) ? r : new Rect(0, 0, 1, 1);
            int vStart = verts.Count;
            foreach (var v in kvp.Value.verts) verts.Add(v + origin);
            foreach (var u in kvp.Value.uv) uv.Add(new Vector2(u.x * rect.width + rect.x, u.y * rect.height + rect.y));
            colors.AddRange(kvp.Value.colors);
            normals.AddRange(kvp.Value.normals);
            foreach (var t in kvp.Value.triangles) triangles.Add(t + vStart);
        }
    }
}
