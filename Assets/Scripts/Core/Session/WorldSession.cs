using UnityEngine;

// Formal "enter world" sequence (successor of WorldSystemTester.Run, Phase 8
// semantics with decisions B and D folded in, design §A.6). World-first,
// player-second, display-takeover, logical-join, gate-last: the frozen logic
// keeps the Phase3-born player unticked until step 9 lands its state; step 11
// mounts the view (display stage), step 12 publishes the logical join and
// step 13 opens the gate on the same frame - the player is never ticked before
// the world (center chunk) is ready, and the camera's first frame already
// shows the ready world. Runs from GameEntryController.Start on the game
// scene: all scene shells (UIManager, WorldRenderer, PlayerRenderer,
// InputHandlerManager) are already Awake. PauseLogic must be true on entry;
// step 13 is the only reset point. Every early return keeps the logic frozen
// and publishes nothing.
public static class WorldSession
{
    public static void StartEnterWorld(string slotFolder)
    {
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
        if(!WorldManager.Instance.TryGetOrGenerateDimension(dimName, out _))return;

        // 8) world readiness: the landing spot is the saved player position when
        // loading, or the WORLD's OWN spawn (world.json v2, decision B) on a
        // fresh world - never a player-side constant. ForceLoadAround builds the
        // center chunk SYNCHRONOUSLY (neighbours queue async). ★ world ready
        Vector3 spawnPos = data != null ? data.position
                                        : WorldSaveManager.Instance.WorldSpawn;
        WorldManager.Instance.ForceLoadAround(spawnPos);

        // 9) player landing - the sequence's ONLY Player-touching point. Fresh
        // worlds reuse RestorePlayer with a synthetic save built from the world
        // spawn (its dimensionId resolves inside RestoreFromSave), so both
        // branches share one landing path instead of the ctor stance.
        if(data != null)
            WorldSaveManager.Instance.RestorePlayer(data);
        else
            WorldSaveManager.Instance.RestorePlayer(new PlayerSaveData
            { position = spawnPos, dimensionId = dimName });

        // 10) mirrors: refresh now - SetRenderDimension's load center reads
        //     PlayerMirror, which would otherwise still hold the pre-restore
        //     position (the last menu-frame value)
        MirrorSync.Instance.Sync();

        // 11) stage 2 - display takeover (decision D): mount the view on the
        //     dimension; the camera follows via PlayerRenderer's LateUpdate (no
        //     code needed). The world is ready and the player settled, still
        //     frozen: the first frame the camera shows is already the ready world.
        if(WorldRenderer.Instance != null)
            WorldRenderer.Instance.SetRenderDimension(dimId);

        // 12) stage 1 - logical join (decision D): publish the join event with
        //     the world ready and the player settled. From the next tick on,
        //     "player entered the ticking world" is a fact.
        EventBus.Instance.Publish(new PlayerJoinEvent());

        // 13) open the gate - the ONLY place that resumes the frozen logic. The
        //     world and the player start ticking together from the very next tick.
        GameLoopDriver.PauseLogic = false;
    }
}
