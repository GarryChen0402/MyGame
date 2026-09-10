using System;
using System.Collections.Generic;
using UnityEngine;

// Static bootstrap entry (Part B §5.3): BeforeSceneLoad now runs only Phase 1
// (core singleton warm-up + GameLoopDriver creation) plus the S1 freeze, then
// hands the former Phase 2-5a chain to BootstrapperDriver - a per-frame
// stepper that shows the loading overlay from the first rendered frame. The
// old synchronous chain is gone on purpose (no dual implementation): it had
// no loading feedback and would drift from the state machine.
public static class GameBootstrap
{
    public static bool IsBootstrapped { get; private set;}
    public static string FailurePhase { get; private set;}

    // Mods found by the stepper's ModSearch step (reflection instantiation in
    // LoadPriority order); kept public as the cross-system mod listing.
    public static List<IMod> Mods = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        if(IsBootstrapped)return;   // domain-reload-off re-play: everything is already built
        try{Phase1_PreCoreSystemInit();}
        catch(Exception e){Fail(nameof(Phase1_PreCoreSystemInit), e); return;}

        // ★ S1 (Part B §2.1): mod registration now spans rendered frames and
        // the player entity is born mid-Registering - without this freeze it
        // would free-fall through the not-yet-loaded world. The only unfreeze
        // is the enter-world gate (WorldSession step 13).
        GameLoopDriver.PauseLogic = true;
        EnsureBootstrapperDriver();
    }

    // Phase 6 continuation: the "enter main menu" step. The bootstrap chain now
    // ends at the driver's PostFreeze unit because scene shells (and the menu)
    // only exist once a scene is loaded - this step executes from the game
    // scene's GameEntryController, when bootstrap completion is guaranteed by
    // the IsBootstrapped hook / the assert below.
    public static bool Phase6_MainMenu()
    {
        if(!IsBootstrapped)
        {
            Debug.LogError("[GameBootstrap] Phase6_MainMenu requires the bootstrap chain (bootstrap incomplete)");
            return false;
        }
        GameLoopDriver.PauseLogic = true;   // freeze the 20Hz logic: no world exists in the menu, and the
                                            // Phase3-born player would otherwise free-fall through nothing
        SaveSlots.EnsureMigrated();         // one-shot legacy save layout migration (safe to re-run)
        return true;
    }

    // Called by BootstrapperDriver's PostFreeze unit BEFORE the completion
    // event is published: subscribers (KeyBindingManager validation, the
    // InputHandlerManager gate, GameEntryController -> Phase6) treat
    // IsBootstrapped as the ready marker.
    public static void MarkBootstrapped()
    {
        IsBootstrapped = true;
    }

    private static void Phase1_PreCoreSystemInit()
    {
        _ = EventBus.Instance;
        _ = PhysicsManager.Instance;
        _ = ResourceSystem.Instance;
        _ = WorldManager.Instance;
        _ = WorldSaveManager.Instance;   // caches persistentDataPath (main thread only)
        _ = InteractionManager.Instance;
        _ = EntityManager.Instance;
        _ = BlockEntityManager.Instance;   // subscribes BlockChanged/ChunkUnloaded before any chunk event
        _ = ItemEntityManager.Instance;
        _ = LootManager.Instance;   // ctor subscribes MobEntityDeathEvent before any kill
        _ = KeyBindingManager.Instance;   // subscribes to BootstrapCompleted (validation + override load)
        EnsureGameLoopDriver();
    }

    private static void EnsureGameLoopDriver()
    {
        if(GameObject.Find("GameLoopDriver") != null)return;   // re-entrancy guard (domain reload off)
        var go = new GameObject("GameLoopDriver");
        go.AddComponent<GameLoopDriver>();
        UnityEngine.Object.DontDestroyOnLoad(go);   // fully qualified: 'Object' collides with System.Object
    }

    private static void EnsureBootstrapperDriver()
    {
        if(GameObject.Find("BootstrapperDriver") != null)return;   // re-entrancy guard (domain reload off)
        var go = new GameObject("BootstrapperDriver");
        go.AddComponent<BootstrapperDriver>();
        UnityEngine.Object.DontDestroyOnLoad(go);
    }

    // Shared failure sink (Phase 1 here, every stepper unit in
    // BootstrapperDriver): record the stage and log; the caller stops the chain.
    public static bool Fail(string phase, Exception e)
    {
        FailurePhase = phase;
        Debug.LogError($"[GameBootstrap] {phase} failed: {e}");
        return false;
    }
}
