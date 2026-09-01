using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Serializes a Chunk to / from the JSON save format (see Docs/世界存档功能设计方案.md).
// Palette holds the non-air state strings present in the subchunk; index 0 is
// implicitly air, so no numeric id ever reaches the save file. Blocks without
// properties serialize as their plain full name ("minecraft:stone"), which is
// exactly save format v1, so old files load without migration. Unknown state
// strings on load map to air (the block was removed from the registry).
[Serializable]
public class ChunkSaveData
{
    public int version = 1;
    public int chunkX;
    public int chunkZ;
    public List<SubChunkSaveData> subchunks = new();
    public List<BlockEntitySaveData> blockEntities;
}

[Serializable]
public class SubChunkSaveData
{
    public int subIndex;
    public List<string> palette = new();
    public List<int> blockData = new();
}

// One block entity in a chunk save. x/y/z are chunk-local (x/z mod 16,
// y = dimension y); type is the BlockEntityDefinition full name. Modules are
// per-module JSON strings in declaration order, matched back by index on load.
[Serializable]
public class BlockEntitySaveData
{
    public int x;
    public int y;
    public int z;
    public string type;
    public List<ModuleSaveData> modules = new();
}

[Serializable]
public class ModuleSaveData
{
    public string module;   // module name (informational)
    public string data;     // the module's own JSON
}

public static class ChunkSerializer
{
    public const int FormatVersion = 3;
    public static readonly int BlockCount = SubChunk.SubChunkBlockSize * SubChunk.SubChunkBlockSize * SubChunk.SubChunkBlockSize;

    // May run on a worker thread: reads a CopyBlockData snapshot only. The
    // blockEntities snapshot is captured on the main thread by the caller.
    public static string Serialize(Chunk chunk, List<BlockEntitySaveData> blockEntities = null)
    {
        var data = new ChunkSaveData
        {
            version = FormatVersion,
            chunkX = chunk.ChunkCoord.x,
            chunkZ = chunk.ChunkCoord.y,
            blockEntities = blockEntities
        };
        int subCount = chunk.MaxSubChunkIndex - chunk.MinSubChunkIndex + 1;
        for (int i = 0; i < subCount; i++)
        {
            SubChunk sub = chunk.GetSubChunk(chunk.MinSubChunkIndex + i);
            if (sub == null) continue;   // empty subchunks are not saved
            data.subchunks.Add(SerializeSubChunk(sub, i));
        }
        return JsonUtility.ToJson(data);
    }

    private static SubChunkSaveData SerializeSubChunk(SubChunk sub, int subIndex)
    {
        ushort[] blocks = sub.CopyBlockData();
        // First pass: dedupe non-air state ids into a palette of state strings.
        var paletteIndex = new Dictionary<ushort, int>();
        var palette = new List<string>();
        for (int i = 0; i < BlockCount; i++)
        {
            ushort stateId = blocks[i];
            if (stateId == 0 || paletteIndex.ContainsKey(stateId)) continue;
            var state = ResourceSystem.Instance.GetState(stateId);
            if (state == null) continue;
            paletteIndex[stateId] = palette.Count + 1;   // palette slot 0 is implicit air
            palette.Add(state.FullName);
        }
        // Second pass: write the per-block palette index.
        var blockData = new List<int>(BlockCount);
        for (int i = 0; i < BlockCount; i++)
        {
            ushort stateId = blocks[i];
            blockData.Add(stateId == 0 ? 0 : paletteIndex.TryGetValue(stateId, out int pidx) ? pidx : 0);
        }
        return new SubChunkSaveData { subIndex = subIndex, palette = palette, blockData = blockData };
    }

    // Fills the chunk from save JSON; unknown state strings become air. The chunk
    // must be empty (freshly constructed) when called. Works for both v1 files
    // (plain block full names) and v2 (state strings), since a block without
    // properties serializes as its plain full name in both formats.
    public static void Deserialize(Chunk chunk, string json)
    {
        var data = JsonUtility.FromJson<ChunkSaveData>(json);
        if (data == null) throw new InvalidDataException("save file is not valid chunk JSON");
        foreach (SubChunkSaveData subData in data.subchunks)
        {
            if (subData.blockData.Count != BlockCount) continue;   // corrupt: drop this subchunk
            SubChunk sub = chunk.GetOrCreateSubChunk(chunk.MinSubChunkIndex + subData.subIndex);
            if (sub == null) continue;

            // palette slot 0 is implicit air; map state strings back to state ids.
            var paletteIds = new ushort[subData.palette.Count + 1];
            for (int p = 0; p < subData.palette.Count; p++)
            {
                if (ResourceSystem.Instance.TryParseStateString(subData.palette[p], out ushort stateId))
                    paletteIds[p + 1] = stateId;
                else
                    Debug.LogWarning($"[ChunkSerializer] unknown block state '{subData.palette[p]}' in chunk save ({data.chunkX},{data.chunkZ}); treated as air");
            }
            for (int i = 0; i < BlockCount; i++)
            {
                int idx = subData.blockData[i];
                if (idx < 0 || idx >= paletteIds.Length) continue;   // corrupt index: leave air
                sub.SetBlockAtRaw(i, paletteIds[idx]);
            }
        }
        // BE data is handed to BlockEntityManager on ChunkLoadedEvent (worker
        // thread only parses strings; instance creation stays on the main thread).
        chunk.PendingBlockEntities = data.blockEntities;
    }

    // Atomic write: write a temp file, then replace the target. File.Replace is
    // only valid when the target exists, so a fresh save falls back to Move.
    // The parent directory is created on demand (dimension dirs don't exist yet
    // on the first save).
    public static void WriteFileAtomic(string path, string content)
    {
        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}
