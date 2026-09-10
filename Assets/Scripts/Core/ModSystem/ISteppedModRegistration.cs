// Optional IMod extension (Part B §5.3): declares RegisterAllResources as N
// independently steppable segments so the bootstrapper can spread the work
// over frames (the heavy parts of Minecraft's registration are synchronous
// Resources.Load calls - the primary frame hogs of the L1 startup).
// Mods that do not implement this run their whole RegisterAllResources as one
// indivisible unit in a single frame (third-party compatibility).
public interface ISteppedModRegistration
{
    int StepCount { get; }

    // stepIndex in 0..StepCount-1; segments run in order, one unit at a time,
    // with no async gap inside a step (registration side effects are main
    // thread and sequential by contract).
    void RegisterStep(int stepIndex);
}
