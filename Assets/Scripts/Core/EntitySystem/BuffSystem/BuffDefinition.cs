using UnityEngine;

// Buff type registry entry (rule §3.1/§3.3/§3.8): one resource per buff type
// (modId:name). Shared - callbacks run per instance but definitions hold no
// per-instance state; everything parameterized lives on the BuffInstance
// subclass that CreateInstance produces.
public abstract class BuffDefinition : ResourceType
{
    // Lifecycle callbacks carry the instance context (rule §3.1): params and
    // Target/Source/Timer are read from inst, never from definition fields.
    public virtual void OnApply(BuffInstance inst) { }             // mount: immediate part (rule §3.1)
    public virtual void OnTick(BuffInstance inst, float dt) { }    // per-frame advance
    public virtual void OnRemove(BuffInstance inst) { }            // cleanup on any removal path

    // Factory - the only instantiation exit (rule §3.8): consumes the mount
    // params JSON into a BuffInstance subclass - typed param fields, Timer
    // init (duration is one of the params) and a canonical VariantKey.
    // Pure parse: no side effects, no Target/Definition wiring.
    public abstract BuffInstance CreateInstance(string paramsJson);

    // Re-serializes the parsed config so the identity key is independent of
    // the caller's JSON formatting (JsonUtility emits declaration order).
    protected static string CanonicalKey<T>(string paramsJson) where T : new()
        => JsonUtility.ToJson(JsonUtility.FromJson<T>(paramsJson));
}
