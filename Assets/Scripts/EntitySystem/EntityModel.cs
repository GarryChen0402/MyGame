using System.Collections.Generic;
using UnityEngine;

// Entity model resource: a tree of CustomCubes. The tree structure (parent ->
// children) lives here in EntityModel, not inside CustomCube.
public class EntityModel : ResourceType
{
    public List<string> Roots;                          // root StringIds (multi-root supported)
    public Dictionary<string, CustomCube> Cubes;        // StringId -> cube
    public Dictionary<string, List<string>> Hierarchy;  // parent StringId -> child StringIds
}

// One cubic part of an EntityModel. Pure leaf data: no child references.
public class CustomCube
{
    public string StringId;
    public Vector3 Size;        // 1m = 1 Block
    public Vector3 Pivot;       // rotation anchor, offset inside the cube (from its low corner)
    public Vector3 Position;    // anchor position relative to the parent GO
    public Dictionary<string, ModelFaceData> Faces;     // keys: top/bottom/front/back/left/right
}
