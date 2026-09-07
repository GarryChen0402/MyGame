using System.Collections.Generic;
using UnityEngine;

// Where an EntityModel's visual content comes from.
public enum EntityModelSourceType
{
    Json,      // our cube-tree json, parsed from SourcePath (a Resources TextAsset)
    Prefab     // a Unity prefab (FBX/Animator or glTF); SourcePath is its Resources path
}

// Entity model resource: a registry descriptor {type, path}. Json sources keep
// the parsed CustomCube tree (Roots/Cubes/Hierarchy), loaded lazily on first
// use via EnsureParsed; Prefab sources carry no cube data and are instantiated
// directly. One registry shape covers both content kinds.
public class EntityModel : ResourceType
{
    public EntityModelSourceType SourceType = EntityModelSourceType.Json;
    public string SourcePath;   // Resources path without extension, e.g. "Models/entity/player"

    // Json-source cube data (empty until EnsureParsed runs; never populated for Prefab).
    public List<string> Roots;                          // root StringIds (multi-root supported)
    public Dictionary<string, CustomCube> Cubes;        // StringId -> cube
    public Dictionary<string, List<string>> Hierarchy;  // parent StringId -> child StringIds

    private bool parseAttempted;

    // Loads and parses the cube tree of a Json source on first call. No-op for
    // Prefab sources and after a successful parse; returns false (with an error
    // log) when the source file is missing or empty, and consumers should then
    // treat the model as broken.
    public bool EnsureParsed()
    {
        if(parseAttempted)return Cubes != null;
        parseAttempted = true;
        if(SourceType != EntityModelSourceType.Json)return true;

        var textAsset = Resources.Load<TextAsset>(SourcePath);
        if(textAsset == null)
        {
            Debug.LogError($"[EntityModel] json source '{SourcePath}' of {FullName} not found (Resources)");
            return false;
        }
        var parsed = EntityModelParser.Parse(textAsset.text);
        Roots = parsed.Roots;
        Cubes = parsed.Cubes;
        Hierarchy = parsed.Hierarchy;
        return Cubes != null;
    }
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
