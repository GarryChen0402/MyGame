using System;
using System.Collections.Generic;
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
    public string ModelId;      // required: model for this variant
    public int RotationX;       // 0/90/180/270, combined via Quaternion.Euler(rotX, rotY, rotZ)
    public int RotationY;
    public int RotationZ;
    public List<AABB> AABBs;    // block-space; defaults to BlockDefinition.AABBs
}

// Immutable runtime state, registered into ResourceSystem.BlockStates once at
// boot (inside Freeze) and read-only after. The registry number id is the
// global state id; FullName is the state string
// ("minecraft:oak_stairs[facing=north,half=bottom]").
public class BlockState : ResourceType
{
    public ushort StateId;              // global state id (stored in chunk data)
    public ushort BlockId;
    public BlockDefinition Block;
    public int LocalStateId;            // index within the block's state list
    public int[] PropertyValueIndices;  // per-property value index, in Properties order
    public string ModelId;
    public int RotationX;
    public int RotationY;
    public int RotationZ;
    public List<AABB> AABBs;            // block-space, already rotated; null = full cube
}

// Normalized classification of a raycast face hit: top/bottom come from the
// face normal, side hits split into upper/lower by the hit point's y offset.
public enum FaceHitType { Top, Bottom, SideUpper, SideLower }

// Inputs for GetStateForPlacement; PlayerYaw is the horizontal facing angle.
// ClickedFaceNormal stays because axis and side-facing still need the side direction.
public struct BlockPlacementContext
{
    public Dimension Dim;
    public Vector3Int ClickedBlockCoord;  // the block that was clicked
    public Vector3Int ClickedFaceNormal;  // face normal of the clicked side
    public FaceHitType HitFace;           // normalized face classification
    public float PlayerYaw;
}
