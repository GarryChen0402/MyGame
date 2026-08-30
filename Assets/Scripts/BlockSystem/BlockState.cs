using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Block state system (vanilla-MC style): every block declares a set of
// properties (e.g. facing, half); the cartesian product of their values is the
// block's state list. The world stores a global state id per block (air = 0).

public class BlockPropertyDefinition
{
    public string Name;
    public string[] Values;   // index 0 is the default value

    public int IndexOfValue(string value) => Array.IndexOf(Values, value);
}

// A state variant maps a property combination to a model + rotation + AABBs.
// Properties are matched partially: a variant with no "half" entry matches
// every half value, so more specific variants must come first in the list.
public class BlockStateVariant
{
    public Dictionary<string, string> Properties = new();
    public string ModelId;      // defaults to BlockDefinition.ModelId
    public int RotationX;       // 0/90/180/270, applied before RotationY
    public int RotationY;
    public List<AABB> AABBs;    // block-space; defaults to BlockDefinition.AABBs
}

// Immutable runtime state, built once at registration and read-only after.
public class BlockState
{
    public ushort StateId;              // global state id (stored in chunk data)
    public ushort BlockId;
    public BlockDefinition Block;
    public int LocalStateId;            // index within the block's state list
    public int[] PropertyValueIndices;  // per-property value index, in Properties order
    public string ModelId;
    public int RotationX;
    public int RotationY;
    public List<AABB> AABBs;            // block-space, already rotated; null = full cube
    public string StateString;          // "minecraft:oak_stairs[facing=north,half=bottom]"
}

// Inputs for GetStateForPlacement; PlayerYaw is the horizontal facing angle.
public struct BlockPlacementContext
{
    public Dimension Dim;
    public Vector3Int ClickedBlockCoord;  // the block that was clicked
    public Vector3Int ClickedFaceNormal;  // face normal of the clicked side
    public Vector3 HitPoint;              // world position where the ray hit the face
    public float PlayerYaw;
}

// Builds every block's states before freeze and answers state id lookups.
// ushort chunk data stores the global state id, so all queries are O(1) arrays.
public class BlockStateRegistry
{
    private readonly List<BlockState> states = new();
    private BlockState[] statesByGlobalId;
    private ushort[] offsetByBlockId;      // blockId -> first global state id
    private ushort[] defaultStateByBlockId;

    public int Count => states.Count;

    // Main thread, once, after mod registration (before Freeze). Requires that
    // minecraft:air was registered first (it becomes state id 0).
    public void Build()
    {
        states.Clear();
        int blockCount = ResourceSystem.Instance.BlockDefinitions.Count;
        offsetByBlockId = new ushort[blockCount];
        defaultStateByBlockId = new ushort[blockCount];
        for (ushort blockId = 0; blockId < blockCount; blockId++)
        {
            if (!ResourceSystem.Instance.BlockDefinitions.TryGetResourceWithNumberId(blockId, out var def)) continue;
            offsetByBlockId[blockId] = (ushort)states.Count;
            BuildStatesFor(def, blockId);
            defaultStateByBlockId[blockId] = offsetByBlockId[blockId];
        }
        statesByGlobalId = states.ToArray();
        if (statesByGlobalId.Length > 0 && statesByGlobalId[0].BlockId != 0)
            Debug.LogError("[BlockStateRegistry] state id 0 must be minecraft:air (register it first)");
    }

    private void BuildStatesFor(BlockDefinition def, ushort blockId)
    {
        if (def.Properties == null || def.Properties.Count == 0)
        {
            CreateState(def, blockId, new int[0], null);
            return;
        }
        int total = 1;
        foreach (var p in def.Properties)
        {
            if (p.Values == null || p.Values.Length == 0)
            {
                Debug.LogError($"[BlockStateRegistry] property '{p.Name}' of {def.FullName} has no values");
                return;
            }
            total *= p.Values.Length;
        }
        if ((long)states.Count + total > ushort.MaxValue)
        {
            Debug.LogError($"[BlockStateRegistry] {def.FullName}: state count exceeds ushort range, block skipped");
            return;
        }
        for (int combo = 0; combo < total; combo++)
        {
            int[] indices = DecodeCombo(def, combo);
            CreateState(def, blockId, indices, MatchVariant(def, indices));
        }
    }

    // Little-endian combo encoding: property 0 is the least significant digit.
    private static int[] DecodeCombo(BlockDefinition def, int combo)
    {
        var indices = new int[def.Properties.Count];
        for (int i = 0; i < def.Properties.Count; i++)
        {
            indices[i] = combo % def.Properties[i].Values.Length;
            combo /= def.Properties[i].Values.Length;
        }
        return indices;
    }

    private static BlockStateVariant MatchVariant(BlockDefinition def, int[] indices)
    {
        if (def.Variants == null) return null;
        foreach (var v in def.Variants)
        {
            bool match = true;
            foreach (var kv in v.Properties)
            {
                int pIdx = -1;
                for (int i = 0; i < def.Properties.Count; i++)
                    if (def.Properties[i].Name == kv.Key) { pIdx = i; break; }
                if (pIdx < 0 || def.Properties[pIdx].IndexOfValue(kv.Value) != indices[pIdx]) { match = false; break; }
            }
            if (match) return v;
        }
        return null;
    }

    private void CreateState(BlockDefinition def, ushort blockId, int[] indices, BlockStateVariant variant)
    {
        var state = new BlockState
        {
            StateId = (ushort)states.Count,
            BlockId = blockId,
            Block = def,
            LocalStateId = states.Count - offsetByBlockId[blockId],
            PropertyValueIndices = indices,
            ModelId = variant != null && !string.IsNullOrEmpty(variant.ModelId) ? variant.ModelId : def.ModelId,
            RotationX = variant?.RotationX ?? 0,
            RotationY = variant?.RotationY ?? 0,
            AABBs = variant?.AABBs ?? def.AABBs,
            StateString = BuildStateString(def, indices)
        };
        state.AABBs = RotateAABBs(state.AABBs, state.RotationX, state.RotationY);
        states.Add(state);
    }

    private static string BuildStateString(BlockDefinition def, int[] indices)
    {
        if (def.Properties == null || def.Properties.Count == 0) return def.FullName;
        var sb = new StringBuilder(def.FullName);
        sb.Append('[');
        for (int i = 0; i < def.Properties.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(def.Properties[i].Name).Append('=').Append(def.Properties[i].Values[indices[i]]);
        }
        sb.Append(']');
        return sb.ToString();
    }

    // Rotates the boxes around the block center; boxes stay axis-aligned because
    // the rotations are 90-degree multiples. Returns null when boxes are null.
    private static List<AABB> RotateAABBs(List<AABB> boxes, int rotX, int rotY)
    {
        if (boxes == null || boxes.Count == 0 || (rotX == 0 && rotY == 0)) return boxes;
        Quaternion rot = Quaternion.Euler(rotX, rotY, 0);   // X before Y, same order as model rotation
        Vector3 center = new(0.5f, 0.5f, 0.5f);
        var result = new List<AABB>(boxes.Count);
        foreach (var b in boxes)
        {
            Vector3 min = rot * (b.MinRange - center) + center;
            Vector3 max = rot * (b.MaxRange - center) + center;
            result.Add(new AABB(
                new Vector3(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Min(min.z, max.z)),
                new Vector3(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y), Mathf.Max(min.z, max.z))));
        }
        return result;
    }

    // ---- queries (read-only, safe for worker threads after Build) ----

    public BlockState GetState(ushort stateId)
    {
        if (statesByGlobalId == null || stateId >= statesByGlobalId.Length) return null;
        return statesByGlobalId[stateId];
    }

    public ushort GetDefaultState(ushort blockId)
        => blockId < defaultStateByBlockId.Length ? defaultStateByBlockId[blockId] : (ushort)0;

    // State id of the given property value combination (little-endian encoding).
    public ushort GetStateId(BlockDefinition def, int[] propertyValueIndices)
    {
        int combo = 0, stride = 1;
        for (int i = 0; i < propertyValueIndices.Length; i++)
        {
            combo += propertyValueIndices[i] * stride;
            stride *= def.Properties[i].Values.Length;
        }
        if (!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId(def.FullName, out ushort blockId)) return 0;
        return (ushort)(offsetByBlockId[blockId] + combo);
    }

    // "minecraft:oak_stairs[facing=north,half=bottom]" -> state id. Blocks
    // without properties are plain full names (compatible with save format v1).
    // Unknown block -> false (caller treats it as air); unknown/missing
    // property values fall back to the block default with a warning.
    public bool TryParseStateString(string stateString, out ushort stateId)
    {
        stateId = 0;
        int br = stateString.IndexOf('[');
        string blockName = br >= 0 ? stateString.Substring(0, br) : stateString;
        if (!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId(blockName, out ushort blockId)) return false;
        if (!ResourceSystem.Instance.BlockDefinitions.TryGetResourceWithNumberId(blockId, out var def) || def == null) return false;
        if (br < 0 || def.Properties == null || def.Properties.Count == 0)
        {
            stateId = GetDefaultState(blockId);
            return true;
        }
        var parsed = new Dictionary<string, string>();
        string inner = stateString.Substring(br + 1, stateString.Length - br - 2);
        foreach (var part in inner.Split(','))
        {
            int eq = part.IndexOf('=');
            if (eq < 0) continue;
            parsed[part.Substring(0, eq).Trim()] = part.Substring(eq + 1).Trim();
        }
        var indices = new int[def.Properties.Count];
        bool unknown = false;
        for (int i = 0; i < def.Properties.Count; i++)
        {
            int vIdx = 0;
            if (parsed.TryGetValue(def.Properties[i].Name, out string val))
            {
                vIdx = def.Properties[i].IndexOfValue(val);
                if (vIdx < 0) { vIdx = 0; unknown = true; }
            }
            else unknown = true;
            indices[i] = vIdx;
        }
        if (unknown)
            Debug.LogWarning($"[BlockStateRegistry] state string '{stateString}' has unknown or missing properties; defaults used");
        stateId = GetStateId(def, indices);
        return true;
    }
}
