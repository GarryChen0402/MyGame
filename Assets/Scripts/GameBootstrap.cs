using System;
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

    public static bool Bootstrap()
    {
        try {Phase1_InitCoreSystems();}
        catch(Exception e){ return Fail("Phase1_InitCoreSystems", e);}
        try {Phase2_LoadMods();}
        catch(Exception e){ return Fail("Phase2_LoadMods", e);}
        try {Phase3_FinalizeResources();}
        catch(Exception e){ return Fail("Phase3_FinalizeResources", e);}
        try {Phase4_PublishCompleted();}
        catch(Exception e){ return Fail("Phase4_PublishCompleted", e);}
        try {Phase5_FreezeRegistries();}
        catch(Exception e){ return Fail("Phase5_FreezeRegistries", e);}

        return true;
    }


    private static void Phase1_InitCoreSystems()
    {
        _ = EventBus.Instance;
        _ = PhysicsManager.Instance;
        _ = ResourceSystem.Instance;
        _ = WorldManager.Instance;
        WorldSaveManager.Instance.Initialize();   // caches persistentDataPath (main thread only)
        _ = InteractionManager.Intance;
    }

    private static void Phase2_LoadMods()
    {
        var mods = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IMod).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (IMod)Activator.CreateInstance(t))
            .OrderBy(m => m.LoadPriority)
            .ToList();
        foreach(var mod in mods)
            mod.RegisterAllResources();
    }

    private static void Phase3_FinalizeResources()
    {
        ResourceSystem.Instance.BuildAtlas();
        if(!ResourceSystem.Instance.BlockDefinitions.ContainsValue("minecraft:air"))
            Debug.LogError("[GameBootstrap] missing required block : minecraft:air");
    }

    private static void Phase4_PublishCompleted()
    {
        IsBootstrapped = true;
        EventBus.Instance.Publish(new BootstrapCompletedEvent { Success = true });
    }

    private static void Phase5_FreezeRegistries()
    {
        ResourceSystem.Instance.Freeze();
    }

    private static bool Fail(string phase, Exception e)
    {
        FailurePhase = phase;
        Debug.LogError($"[GameBootstrap] {phase} failed: {e}");
        return false;
    }
}

public class BootstrapCompletedEvent : GameEvent
{
    public bool Success;
}