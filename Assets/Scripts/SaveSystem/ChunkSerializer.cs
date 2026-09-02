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

// One block entity in a chunk save (container-era format, see design doc §7).
// x/y/z are chunk-local (x/z mod 16, y = dimension y); type is the
// BlockEntityDefinition full name. Data containers are matched back by name
// (definition changes skip with a warning); work containers by list order,
// which mirrors the config order used at assembly.
[Serializable]
public class BlockEntitySaveData
{
    public int x;
    public int y;
    public int z;
    public string type;
    public List<DataContainerSaveData> data = new();
    public List<WorkContainerSaveData> work = new();
}

[Serializable]
public class DataContainerSaveData
{
    public string name;   // container name within the BE
    public string type;   // container type full name (informational)
    public string data;   // the container's own JSON (DataContainer.Serialize)
}

[Serializable]
public class WorkContainerSaveData
{
    public string type;   // container type full name (informational)
    public string data;   // the container's own JSON (WorkContainer.Serialize)
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

    // Main thread only: snapshots one live BE (position + per-container state)
    // into its save entry. Container JSON strings come from each container's
    // Serialize; config order mirrors the assembly order used on restore.
    public static BlockEntitySaveData SerializeBlockEntity(BlockEntity be, Vector3Int localCoord)
    {
        var entry = new BlockEntitySaveData
        {
            x = localCoord.x,
            y = localCoord.y,
            z = localCoord.z,
            type = be.Definition.FullName
        };
        if(be.Definition.DataContainers != null)
        {
            foreach(var cfg in be.Definition.DataContainers)
            {
                if(string.IsNullOrEmpty(cfg.Name))continue;
                if(!be.DataContainers.TryGetValue(cfg.Name, out var container))continue;
                entry.data.Add(new DataContainerSaveData { name = cfg.Name, type = cfg.TypeFullname, data = container.Serialize() });
            }
        }
        for(int i = 0; i < be.WorkContainers.Count; i++)
        {
            string type = null;
            if(be.Definition.WorkContainers != null && i < be.Definition.WorkContainers.Count)
                type = be.Definition.WorkContainers[i].TypeFullname;
            entry.work.Add(new WorkContainerSaveData { type = type, data = be.WorkContainers[i].Serialize() });
        }
        return entry;
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
