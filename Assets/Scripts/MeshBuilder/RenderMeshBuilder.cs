using System;
using System.Collections.Generic;
using UnityEngine;

public static class RenderMeshBuilder
{
    // Occlusion rule for neighbor blocks; currently any present block occludes.
    // Future: only full opaque blocks should occlude (transparent, slabs, fences should not).
    private static bool Occludes(ushort neighborBlockId) => neighborBlockId != 0;

    // Rebuilds the render mesh of a subchunk by appending the visible faces of all blocks.
    // externalBlockQuery: looks up a block outside this subchunk by world position; null means air (0).
    // Returns null if the subchunk contains no blocks.
    public static Mesh ReBuildSubChunkRenderMesh(SubChunk subChunk, Func<int, int, int, ushort> externalBlockQuery = null)
    {
        // Mesh vertices are chunk-local; the chunk transform places them in the world
        Vector3 localOrigin = new Vector3(0, subChunk.YCoord * CoordUtils.BlockSizePerChunk, 0);
        Vector3 worldOrigin = CoordUtils.SubChunkCoordToWorldPos(subChunk.ChunkCoord, subChunk.YCoord);
        Vector3Int worldOriginInt = new Vector3Int((int)worldOrigin.x, (int)worldOrigin.y, (int)worldOrigin.z);

        var verts = new List<Vector3>();
        var uv = new List<Vector2>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        for (int y = 0; y < CoordUtils.BlockSizePerChunk; y++)
        for (int x = 0; x < CoordUtils.BlockSizePerChunk; x++)
        for (int z = 0; z < CoordUtils.BlockSizePerChunk; z++)
        {
            ushort blockId = subChunk.GetBlockAt(x, y, z);
            if (blockId == 0) continue;

            if (!ResourceSystem.Instance.BlockDefinitions.TryGetResourceWithNumberId(blockId, out var blockDef)) continue;
            if (!ResourceSystem.Instance.CustomModels.TryGetResourceWithFullName(blockDef.ModelId, out var model)) continue;

            // Query neighbors along each face direction; the model's own normals define the directions
            var occlusionMask = new Dictionary<string, bool>();
            foreach (var dir in model.GetFaceDirections())
            {
                Vector3Int d = dir.Value;
                int nx = x + d.x, ny = y + d.y, nz = z + d.z;
                ushort neighbor;
                if (CoordUtils.IsCorrectSubChunkLocalCoord(nx, ny, nz))
                {
                    neighbor = subChunk.GetBlockAt(nx, ny, nz);
                }
                else
                {
                    // Neighbor lies in an adjacent subchunk; ask the world layer (null defaults to air)
                    neighbor = externalBlockQuery != null
                        ? externalBlockQuery(worldOriginInt.x + nx, worldOriginInt.y + ny, worldOriginInt.z + nz)
                        : (ushort)0;
                }
                occlusionMask[dir.Key] = Occludes(neighbor);
            }

            // TextureIds[i] matches the model's i-th face, in the order the model JSON was written
            var faceRects = new Dictionary<string, Rect>();
            int faceIndex = 0;
            foreach (var face in model.MeshData)
            {
                string texId = faceIndex < blockDef.TextureIds.Count ? blockDef.TextureIds[faceIndex] : null;
                faceRects[face.Key] = ResolveTextureRect(blockDef.modId, texId);
                faceIndex++;
            }

            model.ExtendModelMesh(localOrigin + new Vector3(x, y, z), verts, uv, colors, normals, triangles, occlusionMask, faceRects);
        }

        if (triangles.Count == 0) return null;

        var mesh = new Mesh
        {
            name = $"subchunk_{subChunk.ChunkCoord.x}_{subChunk.ChunkCoord.y}_{subChunk.YCoord}",
            // A fully solid subchunk can exceed the 65535 limit of 16-bit indices
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Rect ResolveTextureRect(string modId, string texId)
    {
        if (texId != null && ResourceSystem.Instance.Textures.TryGetResourceWithFullName($"{modId}:{texId}", out var tex))
        {
            return tex.AtlasUVRect;
        }
        return new Rect(0, 0, 1, 1);
    }
}
