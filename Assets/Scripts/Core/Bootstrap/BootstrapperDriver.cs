using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

// L1 startup stepper (Part B §5.3): the former synchronous Phase 2-5a chain,
// now advanced one work unit at a time within a per-frame time budget so the
// loading overlay is visible from the first rendered frame and no single
// frame blocks for the whole registration. Created by GameBootstrap's
// BeforeSceneLoad hook as a DontDestroyOnLoad object, with the logic already
// frozen (S1). Work units are never split; at least one unit runs per frame.
// Failure stalls the machine: the step name lands on GameBootstrap.FailurePhase
// and on the overlay, no completion event is published, and PauseLogic stays
// true - the same frozen stance as the old synchronous failure path.
public class BootstrapperDriver : MonoBehaviour
{
    private enum BootStep { ModSearch, Registering, PreFreeze, Freeze, PostFreeze, Done }

    // Soft frame budget: loop checks happen between units only, so one unit
    // may overshoot on its own (a non-stepped mod's whole registration, the
    // atlas pack in PostFreeze - measured during validation).
    private const float FrameBudgetMs = 8f;

    private BootStep step = BootStep.ModSearch;
    private int modIndex;                    // Registering: current mod
    private int stepIndex;                   // Registering: current unit inside that mod
    private int unitCount;                   // total work units (known after ModSearch)
    private int unitDone;
    private bool failed;
    private bool overlayShown;
    private string stage = "";

    private void Update()
    {
        if(failed || step == BootStep.Done)return;

        if(!overlayShown)
        {
            overlayShown = true;   // first rendered frame: the loading page must exist now
            SetStage("Searching mods");
        }

        float deadline = Time.realtimeSinceStartup + FrameBudgetMs / 1000f;
        while(!failed && step != BootStep.Done && Time.realtimeSinceStartup < deadline)
            AdvanceOneUnit();
    }

    private void AdvanceOneUnit()
    {
        try
        {
            switch(step)
            {
                case BootStep.ModSearch: SearchMods(); break;
                case BootStep.Registering: RegisterOneUnit(); break;
                case BootStep.PreFreeze: PublishPreFreezeEvent(); break;
                case BootStep.Freeze: FreezeRegistries(); break;
                case BootStep.PostFreeze: PostFreezeAndComplete(); break;
            }
        }
        catch(Exception e)
        {
            failed = true;
            string phase = step.ToString();
            GameBootstrap.Fail(phase, e);
            // The clock stops (no progress source): percent freezes blank.
            LoadingOverlay.Show("Startup Failed", phase + " (see console)", null);
        }
    }

    // Original Phase 2, verbatim: reflection is one frame's worth of work.
    private void SearchMods()
    {
        var found = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IMod).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => (IMod)Activator.CreateInstance(t))
            .OrderBy(m => m.LoadPriority)
            .ToList();
        foreach(var mod in found)GameBootstrap.Mods.Add(mod);

        unitCount = 1;   // this step
        foreach(var mod in GameBootstrap.Mods)unitCount += UnitCountOf(mod);
        unitCount += 3;  // PreFreeze + Freeze + PostFreeze

        unitDone = 1;
        step = BootStep.Registering;
        UpdateRegisterStage();
    }

    // Original Phase 3, segmented: one unit = one RegisterStep of a stepped
    // mod, or the whole RegisterAllResources of a non-stepped one. Mods stay
    // serial in LoadPriority order across frames.
    private void RegisterOneUnit()
    {
        if(modIndex >= GameBootstrap.Mods.Count)
        {
            step = BootStep.PreFreeze;
            return;   // no unit consumed: mods can be empty
        }

        var mod = GameBootstrap.Mods[modIndex];
        if(IsStepped(mod, out var stepped))stepped.RegisterStep(stepIndex);
        else mod.RegisterAllResources();

        unitDone++;
        stepIndex++;
        if(stepIndex >= UnitCountOf(mod))
        {
            modIndex++;
            stepIndex = 0;
            UpdateRegisterStage();
        }
    }

    // Original Phase 4: kept as its own step even though no subscriber exists
    // today - the event contract stays in place.
    private void PublishPreFreezeEvent()
    {
        SetStage("Finalizing registrations");
        EventBus.Instance.Publish(new ResourceRegisterBeforeFreezeEvent());
        unitDone++;
        step = BootStep.Freeze;
    }

    // Original Phase 5: freeze + cross-table reference validation.
    private void FreezeRegistries()
    {
        SetStage("Validating resources");
        ResourceSystem.Instance.Freeze();
        unitDone++;
        step = BootStep.PostFreeze;
    }

    // Original Phase 5a: PostFreeze (BuildAtlas - the one indivisible heavy
    // frame) -> ready marker -> completion event -> overlay hide. Order
    // matters: event subscribers (GameEntryController -> Phase6) assert
    // IsBootstrapped, so it must be set before the publish.
    private void PostFreezeAndComplete()
    {
        SetStage("Building textures");
        ResourceSystem.Instance.PostFreeze();
        GameBootstrap.MarkBootstrapped();
        EventBus.Instance.Publish(new BootstrapCompletedEvent { Success = true });
        unitDone++;
        step = BootStep.Done;
        LoadingOverlay.Hide();
    }

    private static bool IsStepped(IMod mod, out ISteppedModRegistration stepped)
    {
        stepped = mod as ISteppedModRegistration;
        return stepped != null && stepped.StepCount > 0;
    }

    private static int UnitCountOf(IMod mod)
    {
        return IsStepped(mod, out var stepped) ? stepped.StepCount : 1;
    }

    private void UpdateRegisterStage()
    {
        if(modIndex < GameBootstrap.Mods.Count)
            SetStage($"Registering mods... ({modIndex + 1}/{GameBootstrap.Mods.Count})");
    }

    private void SetStage(string text)
    {
        if(stage == text)return;
        stage = text;
        LoadingOverlay.Show("Loading", text, Progress);
    }

    private float Progress()
    {
        return unitCount > 0 ? (float)unitDone / unitCount : 0f;
    }
}
