using UnityEngine;

// Formal "enter world" sequence (successor of WorldSystemTester.Run; design
// §A.6, Part B §3): a thin session facade over WorldServer. StartEnterWorld
// only freezes the gate and files the request; the server's worker produces a
// WorldEntryResult off the main thread, GameLoopDriver polls it back, and
// OnEntryReady runs the submission chain m1-m4 - slot activation, dimension
// registration, ring submission, then the tracker gate that fires
// FinishEnterWorld (the former steps 9-13, verbatim order). Readiness is async
// throughout: the overlay leaves only through this chain's end. Runs from
// GameEntryController.EnterWorld on the game scene: all scene shells
// (UIManager, WorldRenderer, PlayerRenderer, InputHandlerManager) are already
// Awake and PauseLogic must be true on entry.
public static class WorldSession
{
    private static bool subscribed = false;

    // A request is in flight from StartEnterWorld until its result lands;
    // entryTracker then anchors the m3-m4 phase (re-entry guards).
    private static bool pending = false;
    private static ChunkLoadTracker entryTracker = null;

    public static void StartEnterWorld(string slotFolder)
    {
        if(pending || entryTracker != null)
        {
            Debug.LogWarning("[WorldSession] enter-world already in progress; request ignored");
            return;
        }
        pending = true;

        // 0) gate safety: re-assert the freeze (direct-play / re-entry insurance)
        GameLoopDriver.PauseLogic = true;

        if(!subscribed)
        {
            WorldServer.Instance.WorldEntryReady += OnEntryReady;
            subscribed = true;
        }

        // First overlay stage; the m3 switch swaps in tracker progress.
        LoadingOverlay.Show("Loading World", "Reading save", () => entryTracker?.Progress ?? 0f);
        WorldServer.Instance.RequestEnterWorld(new WorldEntryRequest
        {
            SlotFolder = slotFolder,
            LandingRegionRadius = 1f
        });
    }

    // Main thread (GameLoopDriver → WorldServer.Poll): the worker's result,
    // running the doc's m1-m4 submission chain.
    private static void OnEntryReady(WorldEntryResult result)
    {
        pending = false;
        if(!result.Success)
        {
            FailEntry(result.FailureStage ?? "unknown failure");
            return;
        }

        // m1) activate the slot with the worker's metadata (name / createdTime
        // / spawn caches), restore the seed, stamp last-played.
        WorldSaveManager.Instance.SetActiveWorld(result.Folder, result.Meta);
        WorldManager.Instance.Seed = result.Seed;
        WorldSaveManager.Instance.SaveWorldMeta();

        // m2) register the worker-built dimension; an existing instance wins
        // (re-entry keeps its live chunks) and is picked up by id.
        if(!WorldManager.Instance.IsDimensionExist(result.StartDimensionId))
            WorldManager.Instance.RegisterDimension(result.StartDimensionId, result.BuiltDimension);
        if(!WorldManager.Instance.TryGetDimension(result.StartDimensionId, out var dim))
        {
            FailEntry("m2: dimension registration");
            return;
        }

        // m3) submit the pre-inventoried landing ring asynchronously - no
        // synchronous center chunk; safe while frozen (no player tick can fall
        // through an unready chunk - the tracker's landing gate is the
        // readiness signal). The player focus registers BEFORE the submission:
        // the drop rule tests every focus when a worker finishes, and the
        // focus starts synced on the landing chunk so the first tick after the
        // gate never re-submits the ring.
        Vector2Int centerChunk = Dimension.WorldPosToChunkCoord(result.SpawnPosition);
        WorldManager.Instance.RegisterLoadFocus(new PlayerLoadFocus(centerChunk));
        WorldManager.Instance.SubmitInventoriedRing(dim, result.PendingChunkCoords, result.PendingChunkIsLoad);
        var tracker = new ChunkLoadTracker(dim, centerChunk, result.PendingChunkCoords, result.LandingRegionRadius);
        entryTracker = tracker;
        LoadingOverlay.Show("Loading World", "Building terrain", () => entryTracker?.Progress ?? 0f);
        tracker.RegionReady += () => FinishEnterWorld(result);
        // No-op on a fresh dimension; covers a re-enter whose landing square is
        // already enabled (the event-driven path would never fire then).
        tracker.CheckReadyNow();
    }

    // Failure stance (Part B §5.2): stay frozen, report the stage on the
    // click-through error overlay, and hand control back to the menu. Every
    // guard on the request path is already clear, so entering again works.
    private static void FailEntry(string stage)
    {
        Debug.LogError($"[WorldSession] enter-world failed: {stage}");
        UIManager.Instance.OpenUI("minecraft:main_menu");
        LoadingOverlay.Show("Enter World Failed", stage, null);
    }

    // m4 = former steps 9-13, verbatim order, on the tracker's ready callback
    // (main thread, inside a ChunkLoaded publish).
    private static void FinishEnterWorld(WorldEntryResult result)
    {
        // 9) player landing - the sequence's ONLY Player-touching point. Fresh
        // worlds reuse RestorePlayer with a synthetic save built from the world
        // spawn (its dimensionId resolves inside RestoreFromSave), so both
        // branches share one landing path instead of the ctor stance.
        if(result.PlayerData != null)
            WorldSaveManager.Instance.RestorePlayer(result.PlayerData);
        else
            WorldSaveManager.Instance.RestorePlayer(new PlayerSaveData
            { position = result.SpawnPosition, dimensionId = result.StartDimensionFullName });

        // 10) mirrors: refresh now - SetRenderDimension and the load center
        //     read PlayerMirror, which would otherwise still hold the
        //     pre-restore position (the last menu-frame value)
        MirrorSync.Instance.Sync();

        // 11) stage 2 - display takeover (decision D): mount the view on the
        //     dimension; the camera follows via PlayerRenderer's LateUpdate (no
        //     code needed). The ready ring already fired ChunkLoaded, so the
        //     shells exist before the mount and SyncExistingRenderers re-parents
        //     them under the new dimension host. Still frozen: the first frame
        //     the camera shows is already the ready world.
        if(WorldRenderer.Instance != null)
            WorldRenderer.Instance.SetRenderDimension(result.StartDimensionId);

        // 12) stage 1 - logical join (decision D): publish the join event with
        //     the world ready and the player settled. From the next tick on,
        //     "player entered the ticking world" is a fact.
        EventBus.Instance.Publish(new PlayerJoinEvent());

        // 13) open the gate - the ONLY place that resumes the frozen logic. The
        //     world and the player start ticking together from the very next
        //     tick; the overlay leaves with the freeze.
        GameLoopDriver.PauseLogic = false;
        entryTracker = null;   // re-entry window closes with the gate
        LoadingOverlay.Hide();
    }
}
