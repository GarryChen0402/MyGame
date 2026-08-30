using System.Collections.Generic;
using UnityEngine;

public class BlockDefinition : ResourceType
{
    public Dictionary<string, string> TextureIds;
    // false for glass/water/plants: such blocks never hide the faces behind them.
    public bool IsOpaque = true;
    // false for stairs/slabs etc.: such shapes never fully cover a neighbor's
    // face, so faces behind them must stay visible (conservative culling).
    public bool IsFullCube = true;

    // Block-space collision boxes relative to the block origin; null/empty = full cube.
    public List<AABB> AABBs = null;

    // Block states (vanilla style): properties define the state space, variants
    // map property combinations to model/rotation/AABBs. null = single state.
    public List<BlockPropertyDefinition> Properties = null;
    public List<BlockStateVariant> Variants = null;

    // Initial state when the player places this block. Vanilla 1.16+ stair
    // rules: clicking a side face faces the block the way that face points
    // (the back leans against the clicked block); clicking top/bottom uses the
    // player's horizontal facing. "half" is top when clicking the top face or
    // the upper half of a side face. Complex blocks can override.
    //
    // Our models define "facing" as the direction the step points, while the
    // vanilla rule yields the backing direction, so a top/bottom click must
    // aim the step back at the player (yaw + 180). Side clicks already point
    // the step at the player: the clicked face normal faces the player.
    // Facing steps follow the property's value list: 8 directions (diagonals)
    // are binned every 45 degrees, plain axes every 90 degrees.
    public virtual ushort GetStateForPlacement(BlockPlacementContext ctx)
    {
        var states = ResourceSystem.Instance.BlockStates;
        if (Properties == null || Properties.Count == 0)
        {
            if (!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId(FullName, out ushort blockId)) return 0;
            return states.GetDefaultState(blockId);
        }
        var indices = new int[Properties.Count];
        bool sideClick = ctx.HitFace is FaceHitType.SideUpper or FaceHitType.SideLower;
        for (int i = 0; i < Properties.Count; i++)
        {
            var prop = Properties[i];
            if (prop.Name == "facing" && HasDirections(prop, new[] { "north", "south", "east", "west" }))
                indices[i] = prop.IndexOfValue(sideClick
                    ? FacingNameFromNormal(ctx.ClickedFaceNormal)
                    : FacingName(ctx.PlayerYaw + 180f, prop.Values));
            else if (prop.Name == "axis" && HasDirections(prop, new[] { "x", "y", "z" }))
                indices[i] = prop.IndexOfValue(AxisName(ctx.ClickedFaceNormal));
            else if (prop.Name == "half" && HasDirections(prop, new[] { "top", "lower", "upper", "bottom" }))
                indices[i] = prop.IndexOfValue(ctx.HitFace switch
                {
                    FaceHitType.Top => "top",
                    FaceHitType.SideUpper => "upper",
                    FaceHitType.SideLower => "lower",
                    _ => "bottom"
                });
            else
                indices[i] = 0;
        }
        return states.GetStateId(this, indices);
    }

    // Direction the clicked face points: +Z=south, -Z=north, +X=east, -X=west.
    private static string FacingNameFromNormal(Vector3Int normal)
    {
        if (normal.z > 0) return "south";
        if (normal.z < 0) return "north";
        if (normal.x > 0) return "east";
        if (normal.x < 0) return "west";
        return "south";
    }

    private static bool HasDirections(BlockPropertyDefinition prop, string[] names)
    {
        foreach (var n in names)
            if (prop.IndexOfValue(n) < 0) return false;
        return true;
    }

    private static bool Contains(string[] values, string name)
        => System.Array.IndexOf(values, name) >= 0;

    // Unity: forward = +Z at yaw 0, +X at yaw 90 (counterclockwise viewed from
    // above). Bins the yaw into the values' directions: diagonals (north_east
    // etc.) step every 45°, plain axes every 90°. The bin centers sit on the
    // axes/diagonals, boundaries halfway between them.
    private static string FacingName(float yaw, string[] values)
    {
        bool diagonals = Contains(values, "north_east") && Contains(values, "south_east")
            && Contains(values, "south_west") && Contains(values, "north_west");
        int dirs = diagonals ? 8 : 4;
        string[] order = diagonals
            ? new[] { "south", "south_east", "east", "north_east", "north", "north_west", "west", "south_west" }
            : new[] { "south", "east", "north", "west" };
        float step = 360f / dirs;
        int idx = Mathf.FloorToInt(Mathf.Repeat(yaw + step / 2f, 360f) / step);
        string name = order[idx % dirs];
        int pos = System.Array.IndexOf(values, name);
        return pos >= 0 ? values[pos] : values[0];
    }

    private static string AxisName(Vector3Int normal)
    {
        int ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
        if (ax >= ay && ax >= az) return "x";
        if (ay >= az) return "y";
        return "z";
    }
}
