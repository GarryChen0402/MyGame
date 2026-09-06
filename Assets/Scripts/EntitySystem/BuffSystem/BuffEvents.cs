// All removal sources, one event channel (rule §3.6).
public enum BuffRemoveReason { Expired, Active, Death }

// Add/mount request (rule §3.5): the applier publishes, LivingEntity's main
// handler mounts synchronously on publish (MobEntityHurtEvent pattern).
// Before-stage subscribers may cancel (IsCanceled - immunity) or rewrite the
// payload; v1 has none, so Result turns true once a mount or a merge ran.
public class BuffAddEvent : GameEvent
{
    public LivingEntity Target;
    public BuffDefinition Definition;   // registry entry
    public string ParamsJson;           // variant params; duration is one of them (rule §3.8)
    public Entity Source;               // nullable applier (rule §3.4)
    public bool Result;                 // v1: true after mount or time-merge (default false)
}

// Remove channel (rule §3.6): every removal path publishes this; the main
// handler executes Definition.OnRemove and detaches. The expired ticker and
// the death sweep fire one event per instance (Instance set); active clearing
// targets a definition (all stacked variants/sources) or everything.
public class BuffRemoveEvent : GameEvent
{
    public LivingEntity Target;
    public BuffRemoveReason Reason;
    public BuffInstance Instance;       // precise target (expired tick / per-instance death sweep)
    public BuffDefinition Definition;   // clear-by-definition (all stacked); null + no Instance = clear all
}
