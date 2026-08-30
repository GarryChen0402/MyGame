using System.Collections.Generic;
using UnityEngine;

// Keyframe animation resource driven by a single external parameter t.
// The caller decides what t means (walk distance or time); the animation
// only interpolates keyframes.
public class EntityAnimation : ResourceType
{
    public float Duration;
    public bool Looping;
    public Dictionary<string, List<PartKeyframe>> Parts;   // StringId -> keyframes, sorted by T

    // Writes the evaluated transform of every animated part into output.
    public void Evaluate(float t, Dictionary<string, PartTransform> output)
    {
        if(Looping && Duration > 0f) t %= Duration;
        foreach(var kv in Parts)
        {
            var frames = kv.Value;
            if(frames.Count == 0)continue;
            if(frames.Count == 1)
            {
                output[kv.Key] = frames[0].Transform;
                continue;
            }

            // Binary search for the surrounding keyframes.
            int hi = frames.Count - 1;
            int lo = 0;
            while(lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if(frames[mid].T <= t) lo = mid;
                else hi = mid;
            }
            var a = frames[lo];
            var b = frames[hi];
            float span = b.T - a.T;
            float frac = span <= 0f ? 0f : Mathf.Clamp01((t - a.T) / span);
            output[kv.Key] = PartTransform.Lerp(a.Transform, b.Transform, frac);
        }
    }
}

public struct PartKeyframe
{
    public float T;
    public PartTransform Transform;
}

// Per-part transform: rotation (Euler, degrees), extra position offset, scale.
public struct PartTransform
{
    public Vector3 RotationEuler;
    public Vector3 Position;
    public Vector3 Scale;

    public static PartTransform Identity => new()
    {
        RotationEuler = Vector3.zero,
        Position = Vector3.zero,
        Scale = Vector3.one
    };

    public static PartTransform Lerp(PartTransform a, PartTransform b, float t)
        => new()
        {
            RotationEuler = Vector3.Lerp(a.RotationEuler, b.RotationEuler, t),
            Position = Vector3.Lerp(a.Position, b.Position, t),
            Scale = Vector3.Lerp(a.Scale, b.Scale, t)
        };
}
