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
    private readonly string savesRoot;
    private string worldRootPath;            // active slot directory; null = no active world (menu phase)
    private string activeWorldName;          // world.json name (v2 metadata); the folder name when absent
    private string createdTimeCached;        // read back at slot activation; archived once and never
    private Vector3 spawnCached;             //   overwritten (see SaveWorldMeta), same for spawn below

    // Backward-compatible getter: block IO (chunk paths / autosave) reads this,
    // and during gameplay an active slot always exists before any chunk work.
    public string WorldRootPath => worldRootPath ?? savesRoot;
    public bool HasActiveWorld => worldRootPath != null;

    private WorldSaveManager()
    {
        savesRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Saves");
    }

    // Switches the persistence root to Saves/<folder> and caches the slot's v2
    // fields (name / createdTime / spawn) for later metadata writes. A missing
    // or v1 meta falls back to the folder name / the engine default spawn.
    public void SetActiveWorld(string folder)
        => SetActiveWorld(folder, ReadWorldMetaFromDisk(SlotRootPath(folder)));

    // Same, with the metadata already in hand (the enter-world worker read it
    // off the main thread) - no disk read on this path.
    public void SetActiveWorld(string folder, WorldSaveData meta)
    {
        worldRootPath = SlotRootPath(folder);
        Directory.CreateDirectory(worldRootPath);
        activeWorldName = folder;
        createdTimeCached = null;
        spawnCached = WorldDefaults.DefaultSpawnPosition;
        if(meta == null)return;
        if(!string.IsNullOrEmpty(meta.name))activeWorldName = meta.name;
        createdTimeCached = meta.createdTime;
        // version>=2 with a non-zero spawn -> the world's own spawn (decision
        // B); anything else (v1 leftover / missing field) keeps the engine
        // default.
        if(meta.version >= 2 && meta.spawn != Vector3.zero)spawnCached = meta.spawn;
    }

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

    public void SaveWorldMeta()
    {
        if(!HasActiveWorld)return;   // menu phase guard (T6): never write back to the saves root
        try
        {
            Directory.CreateDirectory(WorldRootPath);
            // A meta-less slot (world.json manually deleted) stamps createdTime
            // on this first write; afterwards only lastPlayedTime ever refreshes.
            createdTimeCached ??= DateTime.UtcNow.ToString("o");
            var data = new WorldSaveData
            {
                seed = WorldManager.Instance.Seed,
                name = activeWorldName,
                createdTime = createdTimeCached,          // cached at activation; never overwritten (T7)
                lastPlayedTime = DateTime.UtcNow.ToString("o"),
                spawn = spawnCached                        // the world's spawn stays archived; the player
                                                           // position goes to player.json only (decision B)
            };
            ChunkSerializer.WriteFileAtomic(Path.Combine(WorldRootPath, "world.json"), JsonUtility.ToJson(data));
        }
        catch(Exception e)
        {
            Debug.LogError($"[WorldSaveManager] save world meta failed: {e}");
        }
    }

    // ---- player ----

    // Applies a save obtained from ReadPlayerSaveDataFromDisk onto Player.Instance -
    // the enter-world sequence's ONLY Player-touching call. Fresh worlds pass
    // a synthetic save built from the world spawn, so both branches share one
    // landing path. Runs only after the world (center chunk) is ready.
    public void RestorePlayer(PlayerSaveData data)
    {
        if(data != null && HasActiveWorld)Player.Instance.RestoreFromSave(data);
    }

    public void SavePlayer()
    {
        if(!HasActiveWorld)return;   // menu phase guard (T6): no slot to write into
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
            if(player.CraftingGrid != null)
                data.craftingGrid = player.CraftingGrid.ExportSave().slots;
            if(ResourceSystem.Instance.DimensionDefinitions.TryGetStringId(player.DimensionId, out string dimName))
                data.dimensionId = dimName;
            var stacks = player.inventory.itemStacks;
            for(int i = 0; i < stacks.Count; i++)
            {
                var stack = stacks[i];
                if(stack == null || stack.IsEmpty()) continue;
                if(!ResourceSystem.Instance.ItemDefinitions.TryGetStringId(stack.itemId, out string itemName)) continue;
                // slotIndex pins each stack to its slot so a reload restores
                // the exact backpack layout instead of shifting items forward.
                data.inventory.Add(new ItemStackSaveData { itemId = itemName, amount = stack.amount, slotIndex = i });
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
        => ChunkPathUnder(WorldRootPath, dimensionFullName, chunkCoord);

    // ---- slot-rooted helpers (worker-safe) ----
    // Pure path math + file IO; no instance state, no main-thread-only Unity
    // APIs beyond JsonUtility - the same off-thread use precedent as
    // ChunkSerializer (chunk load tasks already deserialize on workers). Used
    // by the enter-world worker and by the DTO-injected activation path.

    public string SlotRootPath(string folder) => Path.Combine(savesRoot, folder);

    public static WorldSaveData ReadWorldMetaFromDisk(string worldRootPath)
    {
        try
        {
            string path = Path.Combine(worldRootPath, "world.json");
            if(!File.Exists(path))return null;
            return JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(path));
        }
        catch(Exception e)
        {
            Debug.LogWarning($"[WorldSaveManager] read world meta failed: {e}");
            return null;
        }
    }

    // Reads player.json purely as data; returns null when there is no save
    // (fresh world). Never touches the Player type - the enter-world worker
    // resolves the load position / dimension through this BEFORE the restore
    // step, so reading a save must not summon the player early (touch rule T5).
    public static PlayerSaveData ReadPlayerSaveDataFromDisk(string worldRootPath)
    {
        try
        {
            string path = Path.Combine(worldRootPath, "player.json");
            if(!File.Exists(path))return null;
            var data = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
            if(data == null) throw new InvalidDataException("player.json parse error");
            return data;
        }
        catch(Exception e)
        {
            Debug.LogWarning($"[WorldSaveManager] read player failed: {e}");
            return null;
        }
    }

    public static bool HasChunkSaveUnder(string worldRootPath, string dimensionFullName, Vector2Int chunkCoord)
        => File.Exists(ChunkPathUnder(worldRootPath, dimensionFullName, chunkCoord));

    public static string ChunkPathUnder(string worldRootPath, string dimensionFullName, Vector2Int chunkCoord)
    {
        // ':' and other path-hostile characters are escaped; the directory name
        // is not meant to be human-readable.
        string safe = dimensionFullName.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
        return Path.Combine(worldRootPath, safe, $"chunk_{chunkCoord.x}_{chunkCoord.y}.json");
    }
}

[Serializable]
public class WorldSaveData
{
    public int version = 2;
    public int seed;
    public string name;             // display name; absent in v1 files (JsonUtility leaves null)
    public string createdTime;      // ISO8601 UTC (DateTime.UtcNow.ToString("o")); v1 has none
    public string lastPlayedTime;   // same as createdTime
    public Vector3 spawn;           // world spawn, feet-center pivot (same convention as
                                    // PlayerSaveData.position); zero = unset, readers then use
                                    // the engine default spawn (decision B)
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
    // 2x2 personal crafting grid, slot-pinned like the backpack. Absent in v1
    // saves (JsonUtility leaves the field null) - loaders then keep the grid
    // empty. The crafting result slot is a runtime preview and never saved,
    // matching the workbench work container's persistence contract.
    public List<ItemStackSaveData> craftingGrid;
}

[Serializable]
public class ItemStackSaveData
{
    public string itemId;
    public int amount;
    // Owning inventory slot. -1 (absent in saves written before this field
    // existed) means "no fixed slot": loaders then place the entry into the
    // first empty slot, preserving the old shift-to-front behavior for v1.
    public int slotIndex = -1;
}
