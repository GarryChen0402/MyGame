using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

// World persistence: deterministic generation means only player-modified chunks
// are saved (Chunk.IsModified). Saves are queued and written serially off the
// main thread; OnApplicationQuit blocks until everything is flushed.
public class WorldSaveManager
{
    public static WorldSaveManager Instance {get;} = new();

    // Cached on the main thread: Application.dataPath throws when read from a
    // worker (chunk load tasks run off the main thread).
    private string worldRootPath;
    public string WorldRootPath => worldRootPath;
    private WorldSaveManager()
    {
        worldRootPath ??= Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Saves");
    }
    // public void Initialize()
    // {
    //     if (worldRootPath == null)
    //         worldRootPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Saves");
    // }

    private const float AutosaveIntervalSeconds = 60f;
    private float autosaveTimer;

    // Serialized write queue: one writer thread drains it, so a chunk file is
    // never written concurrently.
    private readonly Queue<ChunkSaveJob> pendingWrites = new();
    private readonly object writeLock = new();
    private bool writerRunning;

    private class ChunkSaveJob
    {
        public Chunk Chunk;
        public string Path;
        public string Json;
    }

    // ---- chunk saves ----

    // Serializes the chunk snapshot on a worker, then queues the file write.
    // Calling thread must be the main thread (checks IsSavedToDisk). BE data is
    // snapshotted here too - containers are main-thread owned - and handed to
    // the worker, which only assembles the file. A chunk unloaded earlier holds
    // its BE snapshot in PendingBlockEntities (see Chunk.UnregisterAllBlockEntities);
    // a live chunk is serialized on the spot.
    public void EnqueueChunkSave(Dimension dim, Chunk chunk)
    {
        if(!chunk.IsModified || chunk.IsSavedToDisk) return;
        string path = ChunkPath(dim.DimensionDefinitionInfo.FullName, chunk.ChunkCoord);
        List<BlockEntitySaveData> blockEntities = chunk.PendingBlockEntities ?? chunk.BuildBlockEntitySaveData();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            string json;
            try { json = ChunkSerializer.Serialize(chunk, blockEntities); }
            catch(Exception e)
            {
                Debug.LogError($"[WorldSaveManager] serialize chunk ({chunk.ChunkCoord}) failed: {e}");
                return;
            }
            lock(writeLock)
            {
                pendingWrites.Enqueue(new ChunkSaveJob { Chunk = chunk, Path = path, Json = json });
                if(!writerRunning)
                {
                    writerRunning = true;
                    ThreadPool.QueueUserWorkItem(_ => WriteLoop());
                }
            }
        });
    }

    private void WriteLoop()
    {
        while(true)
        {
            ChunkSaveJob job;
            lock(writeLock)
            {
                if(pendingWrites.Count == 0) { writerRunning = false; return; }
                job = pendingWrites.Dequeue();
            }
            try
            {
                ChunkSerializer.WriteFileAtomic(job.Path, job.Json);
                job.Chunk.MarkSavedToDisk();
            }
            catch(Exception e)
            {
                Debug.LogError($"[WorldSaveManager] write {job.Path} failed: {e}");
            }
        }
    }

    // Returns true when a save file exists for this chunk (it must be loaded
    // from disk instead of regenerated).
    public bool HasChunkSave(string dimensionFullName, Vector2Int chunkCoord)
        => File.Exists(ChunkPath(dimensionFullName, chunkCoord));

    // Worker thread: fills the chunk from its save file. Returns false when the
    // file is missing or corrupt (caller falls back to generation).
    public bool TryLoadChunkFromDisk(Dimension dim, Chunk chunk)
    {
        string path = ChunkPath(dim.DimensionDefinitionInfo.FullName, chunk.ChunkCoord);
        if(!File.Exists(path)) return false;
        try
        {
            ChunkSerializer.Deserialize(chunk, File.ReadAllText(path));
            chunk.MarkLoadedFromDisk();
            return true;
        }
        catch(Exception e)
        {
            Debug.LogError($"[WorldSaveManager] load chunk ({chunk.ChunkCoord}) failed: {e}");
            return false;
        }
    }

    // ---- autosave ----

    // Called every frame by a MonoBehaviour; scans all dimensions for dirty
    // chunks every AutosaveIntervalSeconds.
    public void Tick(float deltaTime)
    {
        autosaveTimer += deltaTime;
        if(autosaveTimer < AutosaveIntervalSeconds) return;
        autosaveTimer = 0f;
        foreach(var dim in WorldManager.Instance.Dimensions.Values)
        {
            foreach(var chunk in dim.GetEnableChunks()) TryQueueDirty(chunk, dim);
            foreach(var chunk in dim.GetDisableChunks()) TryQueueDirty(chunk, dim);
        }
    }

    private void TryQueueDirty(Chunk chunk, Dimension dim)
    {
        if(chunk.IsModified && !chunk.IsSavedToDisk) EnqueueChunkSave(dim, chunk);
    }

    // ---- quit ----

    // Main thread, on application quit: write every dirty chunk synchronously,
    // drain the pending write queue, then save player + world metadata. The
    // thread has nothing else to do at this point, so blocking is fine.
    public void SaveAllOnQuit()
    {
        try
        {
            foreach(var dim in WorldManager.Instance.Dimensions.Values)
            {
                foreach(var chunk in dim.GetEnableChunks()) WriteChunkSync(dim, chunk);
                foreach(var chunk in dim.GetDisableChunks()) WriteChunkSync(dim, chunk);
            }
            while(true)
            {
                lock(writeLock)
                {
                    if(pendingWrites.Count == 0 && !writerRunning) break;
                }
                Thread.Sleep(1);
            }
        }
        catch(Exception e)
        {
            Debug.LogError($"[WorldSaveManager] quit save failed: {e}");
        }
        SavePlayer();
        SaveWorldMeta();
    }

    private void WriteChunkSync(Dimension dim, Chunk chunk)
    {
        if(!chunk.IsModified || chunk.IsSavedToDisk) return;
        try
        {
            // Same main-thread BE snapshot rule as EnqueueChunkSave.
            List<BlockEntitySaveData> blockEntities = chunk.PendingBlockEntities ?? chunk.BuildBlockEntitySaveData();
            ChunkSerializer.WriteFileAtomic(ChunkPath(dim.DimensionDefinitionInfo.FullName, chunk.ChunkCoord),
                ChunkSerializer.Serialize(chunk, blockEntities));
            chunk.MarkSavedToDisk();
        }
        catch(Exception e)
        {
            Debug.LogError($"[WorldSaveManager] save chunk ({chunk.ChunkCoord}) failed: {e}");
        }
    }

    // ---- world metadata ----

    // Restores WorldManager.Seed from world.json; on first run writes the
    // default seed and returns false.
    public bool LoadWorldMeta()
    {
        string path = Path.Combine(WorldRootPath, "world.json");
        if(!File.Exists(path)) { SaveWorldMeta(); return false; }
        try
        {
            var data = JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(path));
            if(data == null) throw new InvalidDataException("world.json parse error");
            WorldManager.Instance.Seed = data.seed;
            return true;
        }
        catch(Exception e)
        {
            Debug.LogWarning($"[WorldSaveManager] load world meta failed: {e}");
            return false;
        }
    }

    public void SaveWorldMeta()
    {
        try
        {
            Directory.CreateDirectory(WorldRootPath);
            var data = new WorldSaveData { seed = WorldManager.Instance.Seed };
            ChunkSerializer.WriteFileAtomic(Path.Combine(WorldRootPath, "world.json"), JsonUtility.ToJson(data));
        }
        catch(Exception e)
        {
            Debug.LogError($"[WorldSaveManager] save world meta failed: {e}");
        }
    }

    // ---- player ----

    // Restores position/pitch/yaw/dimension/inventory on Player.Instance.
    // Returns false when there is no player save (fresh start).
    public bool LoadPlayer()
    {
        string path = Path.Combine(WorldRootPath, "player.json");
        if(!File.Exists(path)) return false;
        try
        {
            var data = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
            if(data == null) throw new InvalidDataException("player.json parse error");
            Player.Instance.RestoreFromSave(data);
            return true;
        }
        catch(Exception e)
        {
            Debug.LogWarning($"[WorldSaveManager] load player failed: {e}");
            return false;
        }
    }

    public void SavePlayer()
    {
        try
        {
            Directory.CreateDirectory(WorldRootPath);
            Player player = Player.Instance;
            var data = new PlayerSaveData
            {
                position = player.Position,
                pitch = player.pitch,
                yaw = player.yaw,
                inventory = new List<ItemStackSaveData>()
            };
            if(ResourceSystem.Instance.DimensionDefinitions.TryGetStringId(player.DimensionId, out string dimName))
                data.dimensionId = dimName;
            foreach(var stack in player.inventory.itemStacks)
            {
                if(stack == null || stack.IsEmpty()) continue;
                if(!ResourceSystem.Instance.ItemDefinitions.TryGetStringId(stack.itemId, out string itemName)) continue;
                data.inventory.Add(new ItemStackSaveData { itemId = itemName, amount = stack.amount });
            }
            ChunkSerializer.WriteFileAtomic(Path.Combine(WorldRootPath, "player.json"), JsonUtility.ToJson(data));
        }
        catch(Exception e)
        {
            Debug.LogError($"[WorldSaveManager] save player failed: {e}");
        }
    }

    // ---- paths ----

    public string ChunkPath(string dimensionFullName, Vector2Int chunkCoord)
        => Path.Combine(DimensionDir(dimensionFullName), $"chunk_{chunkCoord.x}_{chunkCoord.y}.json");

    private string DimensionDir(string dimensionFullName)
    {
        // ':' and other path-hostile characters are escaped; the directory name
        // is not meant to be human-readable.
        string safe = dimensionFullName.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        return Path.Combine(WorldRootPath, safe);
    }
}

[Serializable]
public class WorldSaveData
{
    public int version = 1;
    public int seed;
}

[Serializable]
public class PlayerSaveData
{
    public int version = 1;
    public Vector3 position;
    public float pitch;
    public float yaw;
    public string dimensionId;
    public List<ItemStackSaveData> inventory = new();
}

[Serializable]
public class ItemStackSaveData
{
    public string itemId;
    public int amount;
}
