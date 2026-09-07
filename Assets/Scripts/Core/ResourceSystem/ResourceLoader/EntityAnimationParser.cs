using System.Collections.Generic;
using UnityEngine;

// Parses EntityAnimation json (JsonUtility) into an EntityAnimation resource.
// Keyframes are grouped per StringId; interpolation happens in EntityAnimation.
public static class EntityAnimationParser
{
    public static EntityAnimation Parse(string json)
    {
        var data = JsonUtility.FromJson<EntityAnimationData>(json);
        var anim = new EntityAnimation
        {
            modId = data.modId,
            name = data.name,
            Duration = data.duration,
            Looping = data.looping,
            Parts = new Dictionary<string, List<PartKeyframe>>()
        };
        foreach(var frame in data.keyframes ?? new KeyframeEntry[0])
        {
            foreach(var part in frame.parts ?? new PartEntry[0])
            {
                if(!anim.Parts.TryGetValue(part.stringId, out var frames))
                {
                    frames = new List<PartKeyframe>();
                    anim.Parts[part.stringId] = frames;
                }
                // JsonUtility leaves unspecified Vector3s at zero; scale should default to one.
                Vector3 scale = part.scale == Vector3.zero ? Vector3.one : part.scale;
                frames.Add(new PartKeyframe
                {
                    T = frame.t,
                    Transform = new PartTransform
                    {
                        RotationEuler = part.rotation,
                        Position = part.position,
                        Scale = scale
                    }
                });
            }
        }
        return anim;
    }
}

[System.Serializable]
public class EntityAnimationData
{
    public string modId;
    public string name;
    public float duration;
    public bool looping;
    public KeyframeEntry[] keyframes;
}

[System.Serializable]
public class KeyframeEntry
{
    public float t;
    public PartEntry[] parts;
}

[System.Serializable]
public class PartEntry
{
    public string stringId;
    public Vector3 rotation;   // Euler angles, degrees
    public Vector3 position;   // extra offset, blocks
    public Vector3 scale;
}
