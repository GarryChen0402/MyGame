using UnityEngine;

public struct AABB
{
    public Vector3 MinRange;
    public Vector3 MaxRange;

    public AABB(Vector3 minRange, Vector3 maxRange)
    {
        MinRange = minRange;
        MaxRange = maxRange;
    }

    public readonly Vector3 Pivot => new ((MinRange.x + MaxRange.x) / 2, MinRange.y, (MinRange.z + MaxRange.z) / 2);

    public readonly AABB ApplyMotion(Vector3 motion)
    {
        return new(
            MinRange + motion, 
            MaxRange + motion
        );
    }
}