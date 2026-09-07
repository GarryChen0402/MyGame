using UnityEngine;

// Demo buff (lifecycle verification only - no numeric-layer effect): heals
// the host at healPerSecond via the existing Heal channel.
public class RegenBuffInstance : BuffInstance
{
    public float HealPerSecond;   // parsed variant param
}

public class RegenBuffDefinition : BuffDefinition
{
    [System.Serializable]
    public class Config { public float duration; public float healPerSecond; }   // duration is a param (rule §3.8)

    public override BuffInstance CreateInstance(string paramsJson)
    {
        var cfg = JsonUtility.FromJson<Config>(paramsJson);
        return new RegenBuffInstance
        {
            HealPerSecond = cfg.healPerSecond,
            Timer = cfg.duration,
            VariantKey = CanonicalKey<Config>(paramsJson)
        };
    }

    public override void OnTick(BuffInstance inst, float dt)
    {
        var regen = (RegenBuffInstance)inst;
        regen.Target.Heal(regen.HealPerSecond * dt);
    }
}
