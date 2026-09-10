using UnityEngine;

public partial class Minecraft
{
    // ---- O: mob model + definition ----
    private void RegisterMobs()
    {
        // Mob Entity
        ResourceSystem.Instance.EntityModels.Register(new EntityModel
        {
            modId = ModId,
            name = "zombie",
            SourceType = EntityModelSourceType.Prefab,
            SourcePath = "GLTF/Zombie1/source/zombie"   // glTFast-imported zombie prefab
        });
        var zombie = new MobDefinition()
        {
            modId = ModId,
            name = "zombie",
            Category = new()
            {
                "undead"
            },
            BaseMaxHealth = 20,
            BaseDamage = 2,
            BaseMoveSpeed = 3,               // species capability values stay on the definition
            AIDefinitionFullName = $"{ModId}:zombie",
            ModelId = $"{ModId}:zombie",
            CollisionBoxes = new()
            {
                new AABB()
                {
                    MinRange = new Vector3(-0.3f, 0, -0.3f),
                    MaxRange = new Vector3( 0.3f, 1.8f, 0.3f)
                }
            },
            LootTables = new() { $"{ModId}:zombie" }
        };
        ResourceSystem.Instance.MobDefinitions.Register(zombie);
    }

    // ---- P: buff types ----
    private void RegisterBuffs()
    {
        // Buff types: demo regeneration buff (lifecycle verification only,
        // heal-through-tick, design doc Docs/Buff系统-代码设计.md §7).
        ResourceSystem.Instance.BuffDefinitions.Register(new RegenBuffDefinition
        { modId = "minecraft", name = "regeneration" });
    }

    // ---- Q: AI spec ----
    private void RegisterAi()
    {
        // AI spec: wiring logic registered once, behavior numbers ride as JSON
        // in ConfigJson (WorkContainer split: logic in C#, numbers as data).
        ResourceSystem.Instance.AIDefinitions.Register(new AIDefinition
        {
            modId = ModId,
            name = "zombie",
            Assembler = ZombieAI.Configure,
            ConfigJson = JsonUtility.ToJson(new ZombieAI.Config
            {
                SenseRange = 16,
                ChaseRange = 24,               // 1.5x sense range: released targets stay chased briefly
                AttackRange = 2.2f,
                AttackInterval = 1.5f,
                TurnSpeed = 120,               // deg/s idle turn + fallback direction snap
                KnockbackStrength = 4,
                WanderMin = 3f,
                WanderMax = 6f
            })
        });
    }
}
