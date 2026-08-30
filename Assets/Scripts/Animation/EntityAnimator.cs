using System.Collections.Generic;
using UnityEngine;

// Applies a set of animation layers to an EntityVisual. Layers blend per part:
// rotations and position offsets add component-wise, scales multiply. Parts
// missing from the visual are skipped (StringId mismatch tolerated); parts
// without any animation keep their base pose.
public static class EntityAnimator
{
    public static void Apply(EntityVisual visual, List<(EntityAnimation anim, float t)> layers)
    {
        var rotation = new Dictionary<string, Vector3>();
        var offset = new Dictionary<string, Vector3>();
        var scale = new Dictionary<string, Vector3>();
        var output = new Dictionary<string, PartTransform>();

        foreach(var (anim, t) in layers)
        {
            output.Clear();
            anim.Evaluate(t, output);
            foreach(var kv in output)
            {
                if(!visual.PartTransforms.ContainsKey(kv.Key))continue;
                if(rotation.TryGetValue(kv.Key, out var r)) rotation[kv.Key] = r + kv.Value.RotationEuler;
                else rotation[kv.Key] = kv.Value.RotationEuler;
                if(offset.TryGetValue(kv.Key, out var o)) offset[kv.Key] = o + kv.Value.Position;
                else offset[kv.Key] = kv.Value.Position;
                if(scale.TryGetValue(kv.Key, out var s)) scale[kv.Key] = Vector3.Scale(s, kv.Value.Scale);
                else scale[kv.Key] = kv.Value.Scale;
            }
        }

        foreach(var kv in visual.PartTransforms)
        {
            Transform part = kv.Value;
            part.localPosition = visual.BasePositions[kv.Key]
                                 + (offset.TryGetValue(kv.Key, out Vector3 off) ? off : Vector3.zero);
            part.localRotation = rotation.TryGetValue(kv.Key, out Vector3 rot) ? Quaternion.Euler(rot) : Quaternion.identity;
            part.localScale = scale.TryGetValue(kv.Key, out Vector3 sc) ? sc : Vector3.one;
        }
    }
}
