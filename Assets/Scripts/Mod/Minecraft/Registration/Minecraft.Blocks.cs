using System.Collections.Generic;
using UnityEngine;

public partial class Minecraft
{
    // ---- D: block definitions ----
    private void BuildBlockDefinitions()
    {
        // BlockDefinition Content
        air = new ()
        {
            modId = ModId,
            name = "air",
            TextureIds = new(){},
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } }
        };
        stoneDefinition = new()
        {
            modId = ModId,
            name = "stone",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:stone",
                ["bottom"] = $"{ModId}:stone",
                ["front"]  = $"{ModId}:stone",
                ["back"]   = $"{ModId}:stone",
                ["left"]   = $"{ModId}:stone",
                ["right"]  = $"{ModId}:stone"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            Hardness = 1.5f,
            LootTables = new() { $"{ModId}:stone" }
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        dirtDefinition = new()
        {
            modId = ModId,
            name = "dirt",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:dirt",
                ["bottom"] = $"{ModId}:dirt",
                ["front"]  = $"{ModId}:dirt",
                ["back"]   = $"{ModId}:dirt",
                ["left"]   = $"{ModId}:dirt",
                ["right"]  = $"{ModId}:dirt"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            Hardness = 1,
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        grassDefinition = new()
        {
            modId = ModId,
            name = "grass",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:grass",
                ["bottom"] = $"{ModId}:dirt",
                ["front"]  = $"{ModId}:grass_side",
                ["back"]   = $"{ModId}:grass_side",
                ["left"]   = $"{ModId}:grass_side",
                ["right"]  = $"{ModId}:grass_side"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            // AABBs = new()
            // {
            //     new AABB(0, 0, 0, 1, 1, 1)
            // }
        };

        // Vanilla-style stairs: 4 facings x 4 halves = 16 states. The stair model
        // faces +Z (south) in its base orientation. half=top/upper flip around X
        // (step on top), which also mirrors the facing, so a Y=180 is added to
        // undo the south<->north swap (rotation applies Y before X). Variants
        // with more property entries must come first.
        var stoneStair = new Dictionary<string, string>();
        foreach(var id in allIds)stoneStair[id] = $"{ModId}:stone";
        stairDefinition = new()
        {
            modId = ModId,
            name = "stone_stair",
            IsFullCube = false,
            TextureIds = stoneStair,
            Properties = new()
            {
                new BlockPropertyDefinition { Name = "facing", Values = new[] { "north", "south", "east", "west" } },
                new BlockPropertyDefinition { Name = "half", Values = new[] { "top", "lower", "upper", "bottom" } }
            },
            Variants = new()
            {
                // half = top / upper: step on top (X=180 flips the bottom shape up)
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "south", ["half"] = "top" }, RotationY = 0   },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "north", ["half"] = "top" }, RotationY = 180 },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "east", ["half"] = "top" },  RotationY = 90  },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "west", ["half"] = "top" },  RotationY = 270 },

                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "south", ["half"] = "upper" }, RotationZ = 180, RotationY = 0   },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "north", ["half"] = "upper" }, RotationZ = 180, RotationY = 180 },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "east", ["half"] = "upper" },  RotationZ = 180, RotationY = 90  },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "west", ["half"] = "upper" },  RotationZ = 180, RotationY = 270 },
                // half = bottom / lower: step on bottom (base shape, no X flip)
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "south", ["half"] = "bottom" }, RotationY = 0   , RotationZ = 180},
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "north", ["half"] = "bottom" }, RotationY = 180 , RotationZ = 180},
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "east", ["half"] = "bottom" },  RotationY = 90  , RotationZ = 180},
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "west", ["half"] = "bottom" },  RotationY = 270 , RotationZ = 180},

                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "south", ["half"] = "lower" }, RotationZ = 0, RotationY = 0   },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "north", ["half"] = "lower" }, RotationZ = 0, RotationY = 180 },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "east", ["half"] = "lower" },  RotationZ = 0, RotationY = 90  },
                new BlockStateVariant { ModelId = stairModel.FullName, Properties = new() { ["facing"] = "west", ["half"] = "lower" },  RotationZ = 0, RotationY = 270 }
            },
            // Base (north-facing, bottom half) shape: full-height back slab plus
            // half-height front step; top variants derive from the X=180 rotation.
            AABBs = new()
            {
                new AABB(new Vector3(0, 0, 0), new Vector3(1, 1, 0.5f)),
                new AABB(new Vector3(0, 0, 0.5f), new Vector3(1, 0.5f, 1))
            }
        };

        cobblestoneDefinition = new()
        {
            modId = ModId,
            name = "cobblestone",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:cobblestone",
                ["bottom"] = $"{ModId}:cobblestone",
                ["front"]  = $"{ModId}:cobblestone",
                ["back"]   = $"{ModId}:cobblestone",
                ["left"]   = $"{ModId}:cobblestone",
                ["right"]  = $"{ModId}:cobblestone"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } }
        };

        // Furnace: static block rendering + a BlockEntity (input/fuel/output
        // inventories + processing module) declared in the BE definition below.
        furnaceDefinition = new()
        {
            modId = ModId,
            name = "furnace",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:furance_top_bottom",
                ["bottom"] = $"{ModId}:furance_top_bottom",
                ["front"]  = $"{ModId}:furance_front_nowork",
                ["back"]   = $"{ModId}:furance_side",
                ["left"]   = $"{ModId}:furance_side",
                ["right"]  = $"{ModId}:furance_side"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            HasBlockEntity = true,
            BlockEntityDefinitionFullName = $"{ModId}:furnace"
        };

        // Crafting table: static cube + a BlockEntity carrying the 3x3 grid,
        // the result slot and the instant crafting logic (BE definition below).
        craftingTableDefinition = new()
        {
            modId = ModId,
            name = "crafting_table",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:crafting_table_top",
                ["bottom"] = $"{ModId}:crafting_table_side",
                ["front"]  = $"{ModId}:crafting_table_front",
                ["back"]   = $"{ModId}:crafting_table_side",
                ["left"]   = $"{ModId}:crafting_table_side",
                ["right"]  = $"{ModId}:crafting_table_side"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            HasBlockEntity = true,
            BlockEntityDefinitionFullName = $"{ModId}:crafting_table"
        };
    }

    // ---- E: block registration + id readback ----
    private void RegisterBlocks()
    {
        ResourceSystem.Instance.BlockDefinitions.Register(air);
        ResourceSystem.Instance.RegisterBlock(stoneDefinition);
        ResourceSystem.Instance.RegisterBlock(dirtDefinition);
        ResourceSystem.Instance.RegisterBlock(grassDefinition);
        grassDefinition.RandomTick = GrassSpreadRandomTick;   // random tick demo: grass spread (design doc 随机刻系统-代码设计 §6)
        ResourceSystem.Instance.RegisterBlock(stairDefinition);
        ResourceSystem.Instance.RegisterBlock(cobblestoneDefinition);
        ResourceSystem.Instance.RegisterBlock(furnaceDefinition);
        ResourceSystem.Instance.RegisterBlock(craftingTableDefinition);

        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:grass", out grassId);
        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:dirt", out dirtId);
        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:stone", out stoneId);
        // ResourceSystem.Instance.BlockDefinitions.Register(stoneDefinition);
        // ResourceSystem.Instance.BlockDefinitions.Register(dirtDefinition);
        // ResourceSystem.Instance.BlockDefinitions.Register(grassDefinition);
    }

    // Grass random tick (design doc 随机刻系统-代码设计 §6): vanilla
    // SpreadableBlock geometry - up to 4 attempts per hit, offsets x/z +-1 and
    // y in [-3, +1] (3x5x3 window, spreads downhill up to 3). The project has
    // no light system, so vanilla's "bright cell above the target" gate is
    // replaced by the structural air gate: only dirt whose above cell is air
    // turns into grass (rules doc §4 R4; a covered dirt stays dirt forever).
    private static void GrassSpreadRandomTick(RandomTickContext ctx)
    {
        if (grassDefaultStateId == ushort.MaxValue || dirtDefaultStateId == ushort.MaxValue)
        {
            grassDefaultStateId = ResourceSystem.Instance.GetDefaultState(grassId);
            dirtDefaultStateId = ResourceSystem.Instance.GetDefaultState(dirtId);
        }
        var rng = ctx.Random;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            int tx = ctx.Pos.x + rng.Next(-1, 2);
            int ty = ctx.Pos.y + rng.Next(-3, 2);
            int tz = ctx.Pos.z + rng.Next(-1, 2);
            var target = new Vector3Int(tx, ty, tz);
            if (ctx.GetStateId(target) != dirtDefaultStateId) continue;   // only plain dirt
            if (!ctx.IsAir(target + Vector3Int.up)) continue;             // air gate above the target
            ctx.TrySetBlockState(target, grassDefaultStateId);
        }
    }
}
