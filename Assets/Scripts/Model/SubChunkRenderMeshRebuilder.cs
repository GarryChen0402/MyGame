using System;
using System.Collections.Generic;
using UnityEngine;

public static class SubChunkRenderMeshRebuilder
{
    public static Mesh RebuildSubChunkRenderMesh(SubChunk sub)
    {
        Vector3 origin = sub.GetSubChunkOrigin();
        List<Vector3> verts = new();
        List<Vector2> uv = new();
        List<Color> colors= new();
        List<Vector3> normals = new();
        List<int> triangles = new();
        for(int y = 0; y < SubChunk.SubChunkBlockSize; y++) 
            for(int x = 0; x < SubChunk.SubChunkBlockSize; x++)
                    for(int z = 0; z < SubChunk.SubChunkBlockSize; z++)
                    {
                        ushort blockId = sub.GetBlockAt(new Vector3Int(x, y, z));
                        if (blockId == 0) continue;
                        if(!ResourceSystem.Instance.BlockDefinitions.TryGetResourceWithNumberId(blockId, out var def))continue;
                        if (def == null) continue;
                        if(!ResourceSystem.Instance.CustomModels.TryGetResourceWithFullName(def.ModelId, out var model)) continue;
                        if (model == null) continue;
                        Dictionary<string, bool> mask = new();
                        Dictionary<string, Rect> faceRects = new();
                        foreach(var kv in model.GetFaceDirections())
                        {
                            var neighborCoord =  sub.SubChunkLocalCoordToDimisionBlockCoord(x, y, z) + kv.Value;
                            var neighborBlockId = QueryBlockIdAt(neighborCoord);
                            mask[kv.Key] = neighborBlockId != 0;

                            ResourceSystem.Instance.Textures.TryGetResourceWithFullName(def.TextureIds[kv.Key], out var rect);
                            faceRects[kv.Key] = rect.AtlasUVRect;
                        }
                        model.ExtendModelMesh
                        (
                            new Vector3(x, y, z),
                            verts, uv, colors, normals, triangles, mask, faceRects
                        );
                    }
        Mesh mesh = new();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.RecalculateBounds();
        
        return mesh;
    }

    // Should be instead by the ChunkManager Func to query the real block in the diminision
    public static Func<Vector3Int, ushort> QueryBlockIdAt = (blockCoord)=> 0;
}