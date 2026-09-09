using UnityEngine;

// Formal "enter world" sequence (successor of WorldSystemTester.Run, Phase 8
// semantics with decisions B and D folded in, design §A.6; Part B §5.2 m-chain).
// World-first, player-second, display-takeover, logical-join, gate-last: the
// frozen logic keeps the Phase3-born player unticked until the ready gate
// lands its state. Readiness itself is now ASYNC (Part B §4.3): the landing
// ring submits without a synchronous center chunk and a ChunkLoadTracker
// (landing 3×3) fires FinishEnterWorld - the former steps 9-13 verbatim, on
// the tracker's main-thread callback. The LoadingOverlay appears only after
// every synchronous early-return, so a failed entry keeps the current frozen
// behavior instead of a stuck overlay. Runs from GameEntryController.EnterWorld
// on the game scene: all scene shells (UIManager, WorldRenderer, PlayerRenderer,
// InputHandlerManager) are already Awake and PauseLogic must be true on entry.
public static class WorldSession
{
    // Anchor of the in-flight entry: re-entry guard (a second StartEnterWorld
    // while one is loading is refused) and the overlay's progress source.
    private static ChunkLoadTracker entryTracker = null;

    public static void StartEnterWorld(string slotFolder)
    {
        if(entryTracker != null)
        {
            Debug.LogWarning("[WorldSession] enter-world already in progress; request ignored");
            return;
        }

        // 0) gate safety: re-assert the freeze (direct-play / re-entry insurance)
        GameLoopDriver.PauseLogic = true;

        // 1) slot resolution (direct-play debug path keeps SampleScene playable
        // standalone). The legacy single-slot layout is migrated first, so a
        // save left in the old fixed directory becomes the "World 1" slot
        // instead of being orphaned next to a freshly created world.
        SaveSlots.EnsureMigrated();
        string folder = slotFolder;
        if(string.IsNullOrEmpty(folder))
        {
            var slots = SaveSlots.ListSlots();
            folder = slots.Count > 0 ? slots[0].Folder
                                     : SaveSlots.CreateWorld("World 1", null)?.Folder;
            Debug.Log($"[WorldSession] direct scene play: entering '{folder}' "
                    + "(no world selected from the main menu)");
            if(folder == null)return;
        }

        // 2-4) activate slot (caches name/createdTime/spawn), restore the seed,
        // stamp last-played
        WorldSaveManager.Instance.SetActiveWorld(folder);
        WorldSaveManager.Instance.LoadWorldMeta();
        WorldSaveManager.Instance.SaveWorldMeta();

        // 5) player save as PURE DATA - no Player touch before step 9
        PlayerSaveData data = WorldSaveManager.Instance.ReadPlayerSaveData();

        // 6) target dimension: the saved dimension when known, else the
        // content-declared start dimension - never a hardcoded mod string
        // (start dims are flagged IsStartDimension at registration, §A.4.7)
        string dimName = null;
        if(data != null && !string.IsNullOrEmpty(data.dimensionId))
            dimName = data.dimensionId;
        if(dimName == null)
        {
            DimensionDefinition startDef = null;
            foreach(var def in ResourceSystem.Instance.DimensionDefinitions.Values)
                if(def.IsStartDimension) { startDef = def; break; }
            if(startDef == null)
            {
                Debug.LogError("[WorldSession] no dimension registered with IsStartDimension=true");
                return;   // do NOT resume logic: the player stays frozen on the menu-safe state
            }
            dimName = startDef.FullName;
        }
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(dimName, out ushort dimId))
        { Debug.LogError($"[WorldSession] dimension '{dimName}' missing id"); return; }

        // 7) dimension instance (chunk data loads/generates asynchronously)
        if(!WorldManager.Instance.TryGetOrGenerateDimension(dimName, out var dim))return;

        // 8) async world readiness (Part B §4.3/§5.2): the landing spot is the
        // saved player position when loading, or the WORLD's OWN spawn
        // (world.json v2, decision B) on a fresh world - never a player-side
        // constant. The landing ring submits fully asynchronously - no
        // synchronous center chunk, safe while the logic is frozen (no player
        // tick can fall through an unready chunk) - and the tracker's landing
        // 3×3 fires FinishEnterWorld instead of the sync center's implicit
        // "returned = ready". ★ gate open deferred to the tracker callback.
        Vector3 spawnPos = data != null ? data.position
                                        : WorldSaveManager.Instance.WorldSpawn;
        Vector2Int centerChunk = Dimension.WorldPosToChunkCoord(spawnPos);
        // Register the player focus BEFORE the submission: the drop rule tests
        // every focus when a worker finishes, and the focus starts synced on
        // the landing chunk so the first tick after the gate never re-submits
        // the ring (and the run-time UpdateLoadCenter only tops up increments).
        WorldManager.Instance.RegisterLoadFocus(new PlayerLoadFocus(centerChunk));
        Vector2Int[] targets = WorldManager.Instance.SubmitLoadRing(dim, centerChunk, WorldManager.ChunkLoadRange);
        var tracker = new ChunkLoadTracker(dim, centerChunk, targets, readyRadius: 1);
        entryTracker = tracker;
        // The overlay appears only after every synchronous early-return above:
        // once shown, the tracker's ready gate is the only exit (an async chunk
        // failure stays frozen, the same stance as the old synchronous path).
        // Progress polls entryTracker from the overlay's own Update.
        LoadingOverlay.Show("Loading World", "Building terrain", () => entryTracker?.Progress ?? 0f);
        tracker.RegionReady += () => FinishEnterWorld(data, spawnPos, dimName, dimId);
        // No-op on a fresh dimension; covers a re-enter whose landing square is
        // already enabled (the event-driven path would never fire then).
        tracker.CheckReadyNow();
    }

    // Former steps 9-13, verbatim order, on the tracker's ready callback (main
    // thread, inside a ChunkLoaded publish).
    private static void FinishEnterWorld(PlayerSaveData data, Vector3 spawnPos, string dimName, ushort dimId)
    {
        // 9) player landing - the sequence's ONLY Player-touching point. Fresh
        // worlds reuse RestorePlayer with a synthetic save built from the world
        // spawn (its dimensionId resolves inside RestoreFromSave), so both
        // branches share one landing path instead of the ctor stance.
        if(data != null)
            WorldSaveManager.Instance.RestorePlayer(data);
        else
            WorldSaveManager.Instance.RestorePlayer(new PlayerSaveData
            { position = spawnPos, dimensionId = dimName });

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
            WorldRenderer.Instance.SetRenderDimension(dimId);

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
