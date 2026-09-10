using System;
using System.Threading;
using UnityEngine;

// V1 in-process WorldServer (Part B §3.3): slot resolution runs on the main
// thread (SaveSlots is main-thread IO), everything after it on one ThreadPool
// worker - w1 meta+player reads, w2 start-dimension resolution (read-only
// registry lookups), w4 dimension construction (audited: the ctor only touches
// frozen registries and stateless generator factories), w3 ring inventory
// (File.Exists classification of the landing ring into load/gen lists). Poll()
// bridges the finished result back to the main thread; workerDone follows the
// ChunkGenTask.IsDown pattern - volatile last write publishes every field
// written before it.
public class InlineWorldServer : WorldServer
{
    private WorldEntryRequest currentRequest;   // non-null while a run is in flight
    private WorldEntryResult currentResult;
    private volatile bool workerDone;

    public override void RequestEnterWorld(WorldEntryRequest request)
    {
        if(currentRequest != null)
        {
            Debug.LogWarning("[InlineWorldServer] enter-world already in progress; request ignored");
            return;
        }

        var result = new WorldEntryResult { LandingRegionRadius = request.LandingRegionRadius };

        // Slot resolution (main thread): legacy migration first, then the
        // direct-play debug path picks the most recent slot or creates
        // "World 1" when none exists yet.
        SaveSlots.EnsureMigrated();
        string folder = request.SlotFolder;
        if(string.IsNullOrEmpty(folder))
        {
            var slots = SaveSlots.ListSlots();
            folder = slots.Count > 0 ? slots[0].Folder : SaveSlots.CreateWorld("World 1", null)?.Folder;
            if(folder == null)
            {
                result.Success = false;
                result.FailureStage = "slot resolution: no slot available";
                RaiseWorldEntryReady(result);
                return;
            }
            Debug.Log($"[InlineWorldServer] direct scene play: entering '{folder}' "
                    + "(no world selected from the main menu)");
        }
        result.Folder = folder;

        // The saves root is main-thread knowledge (Application.dataPath, cached
        // in the ctor); the worker only ever sees the resolved string.
        string rootPath = WorldSaveManager.Instance.SlotRootPath(folder);
        currentRequest = request;
        currentResult = result;
        ThreadPool.QueueUserWorkItem(_ => RunWorker(rootPath, result));
    }

    public override void Poll()
    {
        if(!workerDone)return;
        workerDone = false;   // cleared before delivery: the event may start a new run
        WorldEntryResult result = currentResult;
        currentRequest = null;
        currentResult = null;
        RaiseWorldEntryReady(result);
    }

    private void RunWorker(string rootPath, WorldEntryResult result)
    {
        try
        {
            // w1: slot data as pure reads - metadata and player save; nothing
            // here touches the Player type (touch rule T5).
            WorldSaveData meta = WorldSaveManager.ReadWorldMetaFromDisk(rootPath);
            result.Meta = meta;
            result.Seed = meta != null ? meta.seed : WorldManager.DefaultSeed;
            result.PlayerData = WorldSaveManager.ReadPlayerSaveDataFromDisk(rootPath);

            // w2: target dimension - the saved dimension when known, else the
            // content-declared start dimension (IsStartDimension), never a
            // hardcoded mod string.
            string dimName = null;
            if(result.PlayerData != null && !string.IsNullOrEmpty(result.PlayerData.dimensionId))
                dimName = result.PlayerData.dimensionId;
            if(dimName == null)
            {
                foreach(var startDef in ResourceSystem.Instance.DimensionDefinitions.Values)
                    if(startDef.IsStartDimension) { dimName = startDef.FullName; break; }
                if(dimName == null)
                {
                    Fail(result, "w2: no dimension registered with IsStartDimension=true");
                    return;
                }
            }
            if(!ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(dimName, out ushort dimId))
            {
                Fail(result, $"w2: dimension '{dimName}' missing id");
                return;
            }

            // w4: build the dimension instance here (audit 2026-09-10: the ctor
            // only queries frozen registries and calls a stateless generator
            // factory - no Unity main-thread objects).
            if(!ResourceSystem.Instance.DimensionDefinitions.TryGetResourceWithFullName(dimName, out DimensionDefinition def))
            {
                Fail(result, $"w4: dimension definition '{dimName}' missing");
                return;
            }
            result.StartDimensionFullName = dimName;
            result.StartDimensionId = dimId;
            result.BuiltDimension = new Dimension(def);

            // Landing spot: the saved player position when loading, else the
            // world's own spawn (world.json v2, decision B; engine default on a
            // fresh/v1 slot).
            result.WorldSpawn = meta != null && meta.version >= 2 && meta.spawn != Vector3.zero
                ? meta.spawn : WorldDefaults.DefaultSpawnPosition;
            result.SpawnPosition = result.PlayerData != null ? result.PlayerData.position
                                                             : result.WorldSpawn;

            // w3: ring inventory - classify every landing-ring coordinate as
            // load-from-disk or generate, so the main-thread submission (m3)
            // never probes the disk.
            Vector2Int centerChunk = Dimension.WorldPosToChunkCoord(result.SpawnPosition);
            Vector2Int[] ring = WorldManager.RingCoords(centerChunk, WorldManager.ChunkLoadRange);
            bool[] isLoad = new bool[ring.Length];
            for(int i = 0; i < ring.Length; i++)
                isLoad[i] = WorldSaveManager.HasChunkSaveUnder(rootPath, dimName, ring[i]);
            result.PendingChunkCoords = ring;
            result.PendingChunkIsLoad = isLoad;

            result.Success = true;
        }
        catch(Exception e)
        {
            Debug.LogError($"[InlineWorldServer] worker failed: {e}");
            Fail(result, "worker: unhandled exception");
        }
        finally
        {
            workerDone = true;   // volatile write: publishes every field above
        }
    }

    private static void Fail(WorldEntryResult result, string stage)
    {
        result.Success = false;
        result.FailureStage = stage;
    }
}
