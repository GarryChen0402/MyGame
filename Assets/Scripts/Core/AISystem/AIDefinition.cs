// AI definition resource (design doc AIDefinition参数化配置 §3). One entry
// per AI-capable species: Assembler carries the state-machine wiring logic
// (C#), ConfigJson the per-species behavior numbers - the same split as
// WorkContainerDefinition.Factory vs. its Config contract. Lookup by FullName
// replaces the v1 ModelId branch in MobEntity.Init.
public class AIDefinition : ResourceType
{
    // (MobEntity mob, AIDefinition def) => wire the mob's RootMachine.
    // ZombieAI.Configure is the v1 assembler body; it parses def.ConfigJson
    // into its own [Serializable] Config and injects values into states.
    public System.Action<MobEntity, AIDefinition> Assembler;

    // JsonUtility.ToJson of the assembler's Config type (e.g. ZombieAI.Config).
    public string ConfigJson;
}
