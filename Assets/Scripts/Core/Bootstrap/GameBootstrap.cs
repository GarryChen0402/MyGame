using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
public static class GameBootstrap
{
    public static bool IsBootstrapped { get; private set;}
    public static string FailurePhase { get; private set;}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnBeforeSceneLoad()
    {
        if(IsBootstrapped)return;
        Bootstrap();
    }
    public static List<IMod> Mods = new();
    public static bool Bootstrap()
    {
        // try {Phase1_InitCoreSystems();}
        // catch(Exception e){ return Fail("Phase1_InitCoreSystems", e);}
        // try {Phase2_LoadMods();}
        // catch(Exception e){ return Fail("Phase2_LoadMods", e);}
        // try {Phase3_FinalizeResources();}
        // catch(Exception e){ return Fail("Phase3_FinalizeResources", e);}
        // try {Phase4_PublishCompleted();}
        // catch(Exception e){ return Fail("Phase4_PublishCompleted", e);}
        // try {Phase5_FreezeRegistries();}
        // catch(Exception e){ return Fail("Phase5_FreezeRegistries", e);}

        // V2
        try{Phase1_PreCoreSystemInit();}
        catch(Exception e){return Fail(nameof(Phase1_PreCoreSystemInit), e);}
        try{Phase2_ModSearch();}
        catch(Exception e){return Fail(nameof(Phase2_ModSearch), e);}
        try{Phase3_ResourceRegister();}
        catch(Exception e){return Fail(nameof(Phase3_ResourceRegister), e);}
        try{Phase4_ResourcePostModifyEvent();}
        catch(Exception e){return Fail(nameof(Phase4_ResourcePostModifyEvent), e);}
        try{Phase5_FreezeResourceSystem();}
        catch(Exception e){return Fail(nameof(Phase5_FreezeResourceSystem), e);}
        try{Phase5_a_PostFreezeResourceSystemEvents();}
        catch(Exception e){return Fail(nameof(Phase5_a_PostFreezeResourceSystemEvents), e);}

        return true;
    }

    // Phase 6 continuation: the "enter main menu" step. The static Bootstrap()
    // chain now ends at Phase 5a because BeforeSceneLoad runs before any scene
    // exists - this step executes later from the MainMenu scene's bootstrapper
    // Start (or a future v2 "back to title" re-entry point), when Phases 1-5a
    // are guaranteed complete by both ordering and the assert below.
    public static bool Phase6_MainMenu()
    {
        if(!IsBootstrapped)
        {
            Debug.LogError("[GameBootstrap] Phase6_MainMenu requires Phases 1-5a (bootstrap incomplete)");
            return false;
        }
        GameLoopDriver.PauseLogic = true;   // freeze the 20Hz logic: no world exists in the menu, and the
                                            // Phase3-born player would otherwise free-fall through nothing
        // SaveSlots.EnsureMigrated();      // one-shot legacy save layout migration (safe to re-run);
                                            // backfilled in unit C once SaveSlots.cs exists
        return true;
    }

    private static void Phase5_a_PostFreezeResourceSystemEvents()
    {
        // throw new NotImplementedException();
        ResourceSystem.Instance.PostFreeze();
        EventBus.Instance.Publish(new BootstrapCompletedEvent(){Success = true});
        IsBootstrapped = true;
    }

    private static void Phase5_FreezeResourceSystem()
    {
        ResourceSystem.Instance.Freeze();
    }

    private static void Phase4_ResourcePostModifyEvent()
    {
        EventBus.Instance.Publish(new ResourceRegisterBeforeFreezeEvent());
    }

    private static void Phase3_ResourceRegister()
    {
        foreach(var mod in Mods)mod.RegisterAllResources();
    }

    private static void Phase2_ModSearch()
    {
        var mods = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IMod).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (IMod)Activator.CreateInstance(t))
            .OrderBy(m => m.LoadPriority)
            .ToList();
        foreach(var mod in mods)Mods.Add(mod);
        //sort by order and reference logic
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

    private static bool Fail(string phase, Exception e)
    {
        FailurePhase = phase;
        Debug.LogError($"[GameBootstrap] {phase} failed: {e}");
        return false;
    }
}

