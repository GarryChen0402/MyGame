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
            // Interim source of the first sapling (design doc 树木系统 §7): grass
            // drops one at 5% until natural tree generation lands.
            LootTables = new() { $"{ModId}:grass" }
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

        // Oak log: a pillar. "y" leads the axis value list so the default state
        // is the upright one - tree growth writes GetDefaultState, and a lying
        // default would grow a lying trunk. Placement needs no code: the base
        // GetStateForPlacement already maps the clicked face normal to an axis.
        oakLogDefinition = new()
        {
            modId = ModId,
            name = "oak_log",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:oak_log_top_bottom",
                ["bottom"] = $"{ModId}:oak_log_top_bottom",
                ["front"]  = $"{ModId}:oak_log_side",
                ["back"]   = $"{ModId}:oak_log_side",
                ["left"]   = $"{ModId}:oak_log_side",
                ["right"]  = $"{ModId}:oak_log_side"
            },
            Properties = new()
            {
                new BlockPropertyDefinition { Name = "axis", Values = new[] { "y", "x", "z" } }
            },
            Variants = new()
            {
                // Rotations carry the top/bottom texture onto the pillar ends
                // (Quaternion.Euler order Z-X-Y; up -> +X for Z=270, up -> +Z for X=90).
                new BlockStateVariant { ModelId = cube.FullName, Properties = new() { ["axis"] = "y" } },
                new BlockStateVariant { ModelId = cube.FullName, Properties = new() { ["axis"] = "x" }, RotationZ = 270 },
                new BlockStateVariant { ModelId = cube.FullName, Properties = new() { ["axis"] = "z" }, RotationX = 90 }
            },
            Hardness = 2,
            LootTables = new() { $"{ModId}:oak_log" }
        };

        // Oak planks: the log breakdown product of the tree chain.
        oakPlanksDefinition = new()
        {
            modId = ModId,
            name = "oak_planks",
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:oak_plank",
                ["bottom"] = $"{ModId}:oak_plank",
                ["front"]  = $"{ModId}:oak_plank",
                ["back"]   = $"{ModId}:oak_plank",
                ["left"]   = $"{ModId}:oak_plank",
                ["right"]  = $"{ModId}:oak_plank"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            Hardness = 2,
            LootTables = new() { $"{ModId}:oak_planks" }
        };

        // Oak leaves: full cube with a holed texture. IsOpaque=false - the holes
        // show the neighbours, so this block must never cull their faces (same
        // semantics as glass/water in the IsOpaque comment).
        oakLeavesDefinition = new()
        {
            modId = ModId,
            name = "oak_leaves",
            IsOpaque = false,
            TextureIds = new()
            {
                ["top"]    = $"{ModId}:oak_leaves",
                ["bottom"] = $"{ModId}:oak_leaves",
                ["front"]  = $"{ModId}:oak_leaves",
                ["back"]   = $"{ModId}:oak_leaves",
                ["left"]   = $"{ModId}:oak_leaves",
                ["right"]  = $"{ModId}:oak_leaves"
            },
            Variants = new() { new BlockStateVariant { ModelId = cube.FullName } },
            Hardness = 0.2f,
            LootTables = new() { $"{ModId}:oak_leaves" }
        };

        // Oak sapling: cross model, no random tick until RegisterBlocks wires
        // SaplingGrowRandomTick (it needs the id readback to have run).
        oakSaplingDefinition = new()
        {
            modId = ModId,
            name = "oak_sapling",
            IsOpaque = false,
            IsFullCube = false,
            TextureIds = new()
            {
                ["cross_a"]      = $"{ModId}:oak_sapling",
                ["cross_a_back"] = $"{ModId}:oak_sapling",
                ["cross_b"]      = $"{ModId}:oak_sapling",
                ["cross_b_back"] = $"{ModId}:oak_sapling"
            },
            Variants = new() { new BlockStateVariant { ModelId = crossModel.FullName } },
            // Post-sized box: ray hits (aiming/breaking) read the block shape,
            // and a null AABB list would fall back to the full cube.
            AABBs = new()
            {
                new AABB(new Vector3(0.3f, 0, 0.3f), new Vector3(0.7f, 0.4f, 0.7f))
            },
            Hardness = 0,
            LootTables = new() { $"{ModId}:oak_sapling" }
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
        ResourceSystem.Instance.RegisterBlock(oakLogDefinition);
        ResourceSystem.Instance.RegisterBlock(oakLeavesDefinition);
        ResourceSystem.Instance.RegisterBlock(oakSaplingDefinition);
        oakSaplingDefinition.RandomTick = SaplingGrowRandomTick;   // random tick: sapling growth (design doc 树木系统 §5)
        // New blocks append at the segment tail: earlier registrations keep
        // their state ids (save compat).
        ResourceSystem.Instance.RegisterBlock(oakPlanksDefinition);

        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:grass", out grassId);
        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:dirt", out dirtId);
        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:stone", out stoneId);
        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:oak_log", out oakLogId);
        ResourceSystem.Instance.BlockDefinitions.TryGetNumberId($"{ModId}:oak_leaves", out oakLeavesId);
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

    // Sapling random tick (design doc 树木系统 §5): the only source of the
    // first wood while natural tree generation is postponed. Vanilla random
    // tick semantics - a structural gate (soil below, free space above) and a
    // chance roll; the project has no light system, so the roll is the pacing
    // knob instead of vanilla's light-level gate.
    private static void SaplingGrowRandomTick(RandomTickContext ctx)
    {
        if (grassDefaultStateId == ushort.MaxValue || dirtDefaultStateId == ushort.MaxValue
            || oakLogDefaultStateId == ushort.MaxValue || oakLeavesDefaultStateId == ushort.MaxValue)
        {
            grassDefaultStateId = ResourceSystem.Instance.GetDefaultState(grassId);
            dirtDefaultStateId = ResourceSystem.Instance.GetDefaultState(dirtId);
            oakLogDefaultStateId = ResourceSystem.Instance.GetDefaultState(oakLogId);
            oakLeavesDefaultStateId = ResourceSystem.Instance.GetDefaultState(oakLeavesId);
        }
        ushort soil = ctx.GetStateId(ctx.Pos + Vector3Int.down);
        if (soil != dirtDefaultStateId && soil != grassDefaultStateId) return;
        if (ctx.Random.Next(9) != 0) return;

        var cells = BuildTreeOffsets(4 + ctx.Random.Next(3));
        // All-or-nothing precheck: every cell must be air (except the sapling's
        // own cell, which the trunk replaces) and every touched chunk must be
        // loaded. Random ticks run on the main thread and write nothing in
        // between, so a passed precheck cannot fail halfway.
        foreach (var cell in cells)
        {
            var pos = ctx.Pos + cell.Offset;
            if (pos != ctx.Pos && !ctx.IsAir(pos)) return;
            var chunkCoord = Dimension.DimensionCoordToChunkCoord(pos);
            if (!ctx.Dim.IsChunkEnabled(chunkCoord) || !ctx.Dim.TryGetChunk(chunkCoord, out _)) return;
        }
        // Trunk bottom-up first (the lowest cell replaces the sapling), then the
        // canopy; the shape keeps the trunk column out of the canopy, so the
        // wood is never overwritten by leaves.
        foreach (var cell in cells)
            if (cell.IsLog) ctx.TrySetBlockState(ctx.Pos + cell.Offset, oakLogDefaultStateId);
        foreach (var cell in cells)
            if (!cell.IsLog) ctx.TrySetBlockState(ctx.Pos + cell.Offset, oakLeavesDefaultStateId);
    }

    // One cell of a tree shape: offset from the sapling cell plus which block
    // goes there.
    private struct TreeCell
    {
        public Vector3Int Offset;
        public bool IsLog;
    }

    // Oak shape as a pure offset table (design doc 树木系统 §4) - no world
    // access, so the postponed natural-tree generation can reuse this exact
    // shape instead of growing a second, drifting copy. Trunk column up to
    // h-1, canopy at h-2/h-1 (5x5 minus corners minus the trunk column),
    // h (3x3 plus shape, the trunk's top cap) and h+1 (apex): 46 leaves.
    private static List<TreeCell> BuildTreeOffsets(int height)
    {
        var cells = new List<TreeCell>();
        for (int y = 0; y < height; y++)
            cells.Add(new TreeCell { Offset = new Vector3Int(0, y, 0), IsLog = true });
        for (int y = height - 2; y <= height; y++)
        {
            int radius = y == height ? 1 : 2;
            for (int dx = -radius; dx <= radius; dx++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (Mathf.Abs(dx) == radius && Mathf.Abs(dz) == radius) continue;   // corners
                    if (dx == 0 && dz == 0 && y < height) continue;                     // trunk column stays wood
                    cells.Add(new TreeCell { Offset = new Vector3Int(dx, y, dz), IsLog = false });
                }
        }
        cells.Add(new TreeCell { Offset = new Vector3Int(0, height + 1, 0), IsLog = false });
        return cells;
    }
}
