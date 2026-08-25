using System.Collections.Generic;
using UnityEngine;

public static class SubChunkRenderMeshRebuilder
{
    public static Mesh RebuildSubChunkRenderMesh(SubChunk sub)
    {
        Vector3 origin = sub.GetSubChunkOrigin();
        
        for(int y = 0; y < SubChunk.SubChunkBlockSize; y++) 
            for(int x = 0; x < SubChunk.SubChunkBlockSize; x++)
                    for(int z = 0; z < SubChunk.SubChunkBlockSize; z++)
                    {
                        ushort blockId = sub.GetBlockAt(new Vector3Int(x, y, z));
                        if (blockId == 0) continue;
                        if(!ResourceSystem.Instance.BlockDefinitions.TryGetResourceWithNumberId(blockId, out var def))continue;
                        if (def == null) continue;
                        if(!ResourceSystem.Instance.CustomModels.TryGetResourceWithFullName(def.FullName, out var model)) continue;
                        if (model == null) continue;
                        Dictionary<string, bool> mask = new Dictionary<string, bool>();
                        foreach(var kv in model.GetFaceDirections())
                        {
                            var neighbor = 
                        }
                    }
        return null;
    }
}