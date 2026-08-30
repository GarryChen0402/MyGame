using System.Collections.Generic;
using UnityEngine;

public class BlockDefinition : ResourceType
{
    public string ModelId;
    public Dictionary<string, string> TextureIds;
    // false for glass/water/plants: such blocks never hide the faces behind them.
    public bool IsOpaque = true;

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
    // the upper half of a side face (hit point offset > 0.5). Complex blocks
    // can override.
    //
    // Our models define "facing" as the direction the step points, while the
    // vanilla rule yields the backing direction, so a top/bottom click must
    // aim the step back at the player (yaw + 180). Side clicks already point
    // the step at the player: the clicked face normal faces the player.
    public virtual ushort GetStateForPlacement(BlockPlacementContext ctx)
    {
        var states = ResourceSystem.Instance.BlockStates;
        if (Properties == null || Properties.Count == 0)
        {
            if (!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId(FullName, out ushort blockId)) return 0;
            return states.GetDefaultState(blockId);
        }
        var indices = new int[Properties.Count];
        bool sideClick = ctx.ClickedFaceNormal.y == 0;
        for (int i = 0; i < Properties.Count; i++)
        {
            var prop = Properties[i];
            if (prop.Name == "facing" && HasDirections(prop, new[] { "north", "south", "east", "west" }))
                indices[i] = prop.IndexOfValue(sideClick
                    ? FacingNameFromNormal(ctx.ClickedFaceNormal)
                    : FacingName(ctx.PlayerYaw + 180f));
            else if (prop.Name == "axis" && HasDirections(prop, new[] { "x", "y", "z" }))
                indices[i] = prop.IndexOfValue(AxisName(ctx.ClickedFaceNormal));
            else if (prop.Name == "half" && prop.IndexOfValue("top") >= 0 && prop.IndexOfValue("bottom") >= 0)
                indices[i] = prop.IndexOfValue(ctx.ClickedFaceNormal.y > 0
                    || (sideClick && ctx.HitPoint.y - ctx.ClickedBlockCoord.y > 0.5f)
                        ? "top" : "bottom");
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

    // Unity: forward = +Z at yaw 0, +X at yaw 90 (counterclockwise viewed from above).
    private static string FacingName(float yaw)
    {
        float a = Mathf.Repeat(yaw, 360f);
        if (a >= 45f && a < 135f) return "east";
        if (a >= 135f && a < 225f) return "north";
        if (a >= 225f && a < 315f) return "west";
        return "south";
    }

    private static string AxisName(Vector3Int normal)
    {
        int ax = Mathf.Abs(normal.x), ay = Mathf.Abs(normal.y), az = Mathf.Abs(normal.z);
        if (ax >= ay && ax >= az) return "x";
        if (ay >= az) return "y";
        return "z";
    }
}
