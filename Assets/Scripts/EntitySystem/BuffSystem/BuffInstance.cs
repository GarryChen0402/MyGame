// Mount entry (design doc Docs/Buff系统-代码设计.md §2.2): one entry per
// distinct applied buff. Base fields are the mount frame + identity; per-type
// params and handle storage live on the subclass the definition's factory
// creates.
public class BuffInstance
{
    // Remaining > 5000s counts as the infinite domain - the timer never
    // decrements, OnTick keeps running (rule §3.6).
    public const float InfiniteSeconds = 5000f;

    public BuffDefinition Definition;   // registry entry (set by the mount handler)
    public LivingEntity Target;         // host (set by the mount handler)
    public Entity Source;               // applier, nullable (rule §3.4)
    public float Timer;                 // remaining seconds; > InfiniteSeconds: no decrement (rule §3.6)
    public string VariantKey;           // canonical params key - identity component (rule §4)
}
