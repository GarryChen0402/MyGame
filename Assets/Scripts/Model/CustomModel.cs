using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CustomModel : ResourceType
{
    // public string ModelName;
    public Dictionary<string, ModelFaceData> MeshData;

    private Dictionary<string, Vector3Int> faceDirectionsCache;

    // Returns face name -> direction of the outward normal, rounded to integer axes.
    // Cached lazily because MeshData never changes after parsing.
    // Thread-safe for concurrent first access (worker threads build the mesh):
    // build into a local dict and publish once, so no two threads ever mutate
    // the same Dictionary instance (a losing build is simply discarded).
    public Dictionary<string, Vector3Int> GetFaceDirections()
    {
        if (faceDirectionsCache != null) return faceDirectionsCache;
        var cache = new Dictionary<string, Vector3Int>();
        foreach (var kvp in MeshData)
        {
            Vector3 n = kvp.Value.normals.Count > 0 ? kvp.Value.normals[0] : Vector3.zero;
            cache[kvp.Key] = new Vector3Int(
                Mathf.RoundToInt(n.x), Mathf.RoundToInt(n.y), Mathf.RoundToInt(n.z));
        }
        faceDirectionsCache = cache;
        return cache;
    }

    // Appends vertex data of all non-occluded faces to the given lists.
    // occlusionMask: face name -> whether a neighbor blocks that direction; faces occluded and
    //   canBeOccluded are skipped. faceRects: face name -> atlas region for UV mapping.
    // rotX/rotY/rotZ (0/90/180/270): rotate the model around the block center
    // (0.5,0.5,0.5) via Quaternion.Euler(rotX, rotY, rotZ) (Unity order: Z, then
    // X, then Y). Vertices and normals rotate; UVs and face names stay.
    public void ExtendModelMesh(Vector3 origin, List<Vector3> verts, List<Vector2> uv, List<Color> colors,
        List<Vector3> normals, List<int> triangles, Dictionary<string, bool> occlusionMask = null,
        Dictionary<string, Rect> faceRects = null, int rotX = 0, int rotY = 0, int rotZ = 0)
    {
        bool rotated = rotX != 0 || rotY != 0 || rotZ != 0;
        Quaternion rot = rotated ? Quaternion.Euler(rotX, rotY, rotZ) : Quaternion.identity;
        Vector3 center = new(0.5f, 0.5f, 0.5f);
        foreach (var kvp in MeshData)
        {
            if (occlusionMask != null && occlusionMask.TryGetValue(kvp.Key, out bool occluded) && occluded && kvp.Value.canBeOccluded)
                continue;

            Rect rect = faceRects != null && faceRects.TryGetValue(kvp.Key, out Rect r) ? r : new Rect(0, 0, 1, 1);
            int vStart = verts.Count;
            if (rotated)
            {
                foreach (var v in kvp.Value.verts) verts.Add(rot * (v - center) + center + origin);
                foreach (var n in kvp.Value.normals) normals.Add(rot * n);
            }
            else
            {
                foreach (var v in kvp.Value.verts) verts.Add(v + origin);
                normals.AddRange(kvp.Value.normals);
            }
            foreach (var u in kvp.Value.uv) uv.Add(new Vector2(u.x * rect.width + rect.x, u.y * rect.height + rect.y));
            colors.AddRange(kvp.Value.colors);
            foreach (var t in kvp.Value.triangles) triangles.Add(t + vStart);
        }
    }

    // Rotates a face direction by the same convention as ExtendModelMesh.
    public static Vector3Int RotateDirection(Vector3Int dir, int rotX, int rotY, int rotZ = 0)
    {
        if (rotX == 0 && rotY == 0 && rotZ == 0) return dir;
        Vector3 d = Quaternion.Euler(rotX, rotY, rotZ) * (Vector3)dir;
        return new Vector3Int(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.y), Mathf.RoundToInt(d.z));
    }

    public List<string> GetAllFaceId()
    {
        return MeshData.Keys.ToList();
    }
}
