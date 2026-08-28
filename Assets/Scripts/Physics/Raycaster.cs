using UnityEngine;

public readonly struct RaycastHit
{
    public readonly Vector3Int BlockDimensionCoord;
    public readonly Vector3 Normal;
    public readonly Vector3 HitPoint;
    public readonly float Distance;
    public readonly bool IsHit;

    public RaycastHit(Vector3Int coord, Vector3 norm, Vector3 hitpoint, float dis)
    {
        BlockDimensionCoord = coord;
        Normal = norm;
        HitPoint = hitpoint;
        Distance = dis;
        IsHit = true;
    }
}

public static class Raycaster
{
    public static bool Raycast(Dimension dim, Vector3 origin, Vector3 dir, float maxDistance, out RaycastHit hit)
    {
        hit = default;
        Vector3Int originCoord = Dimension.WorldPosToDimensionCoord(origin);
        int   stepX   = dir.x >= 0 ? 1 : -1;
        float tDeltaX = Mathf.Abs(1f / dir.x);   // +inf when direction.x == 0
        float tMaxX   = NextBoundaryTime(origin.x, originCoord.x, dir.x, tDeltaX);
        int   stepY   = dir.y >= 0 ? 1 : -1;
        float tDeltaY = Mathf.Abs(1f / dir.y);
        float tMaxY   = NextBoundaryTime(origin.y, originCoord.y, dir.y, tDeltaY);
        int   stepZ   = dir.z >= 0 ? 1 : -1;
        float tDeltaZ = Mathf.Abs(1f / dir.z);
        float tMaxZ   = NextBoundaryTime(origin.z, originCoord.z, dir.z, tDeltaZ);

        float t = 0f;
        while(t <= maxDistance)
        {
            if(dim.GetBlockAt(originCoord) != 0)
            {
                float bestT = float.PositiveInfinity;
                Vector3 bestNormal = Vector3.zero;
                PhysicsManager.ForEachBlockCollisionBox(dim , originCoord, box =>
                {
                    if(RayAABB(origin, dir, box, out float boxT, out Vector3 boxNormal) && boxT < bestT)
                    {
                        bestT = boxT;
                        bestNormal = boxNormal;
                    }
                });

                if(bestT <= maxDistance)
                {
                    hit = new RaycastHit(originCoord, bestNormal, origin + dir * bestT, bestT);
                    return true;
                }
            }

            if(tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                originCoord.x += stepX;
                t = tMaxX;
                tMaxX += tDeltaX;
            }
            else if(tMaxY < tMaxZ)
            {
                originCoord.y += stepY;
                t = tMaxY;
                tMaxY += tDeltaY;
            }
            else
            {
                originCoord.z += stepZ;
                t = tMaxZ;
                tMaxZ += tDeltaZ;
            }
        }
        return false;

    }
    private static float NextBoundaryTime(float origin, int coord, float dir, float tDelta)
    {
        if(dir == 0) return float.PositiveInfinity;
        // The boundary sits on the near side of the current cell along the ray;
        // the distance to it is always positive (the eye is inside the cell).
        float boundary = dir > 0 ? coord + 1 : coord;
        return Mathf.Abs(boundary - origin) * tDelta;
    }

    private static bool RayAABB(Vector3 origin, Vector3 dir, AABB box, out float t, out Vector3 normal)
    {
        t = 0f;
        normal = Vector3.zero;
        float tmin = 0f, tmax = float.PositiveInfinity;
        int entryAxis = -1;

        if(Mathf.Abs(dir.x) < 1e-6f)
        {
            if(origin.x < box.MinRange.x || origin.x > box.MaxRange.x) return false;
        }
        else
        {
            float t1 = (box.MinRange.x - origin.x) / dir.x;
            float t2 = (box.MaxRange.x - origin.x) / dir.x;
            float lo = Mathf.Min(t1, t2), hi = Mathf.Max(t1, t2);
            if(lo > tmin) { tmin = lo; entryAxis = 0; }
            tmax = Mathf.Min(tmax, hi);
            if(tmin > tmax) return false;
        }
        if(Mathf.Abs(dir.y) < 1e-6f)
        {
            if(origin.y < box.MinRange.y || origin.y > box.MaxRange.y) return false;
        }
        else
        {
            float t1 = (box.MinRange.y - origin.y) / dir.y;
            float t2 = (box.MaxRange.y - origin.y) / dir.y;
            float lo = Mathf.Min(t1, t2), hi = Mathf.Max(t1, t2);
            if(lo > tmin) { tmin = lo; entryAxis = 1; }
            tmax = Mathf.Min(tmax, hi);
            if(tmin > tmax) return false;
        }
        if(Mathf.Abs(dir.z) < 1e-6f)
        {
            if(origin.z < box.MinRange.z || origin.z > box.MaxRange.z) return false;
        }
        else
        {
            float t1 = (box.MinRange.z - origin.z) / dir.z;
            float t2 = (box.MaxRange.z - origin.z) / dir.z;
            float lo = Mathf.Min(t1, t2), hi = Mathf.Max(t1, t2);
            if(lo > tmin) { tmin = lo; entryAxis = 2; }
            tmax = Mathf.Min(tmax, hi);
            if(tmin > tmax) return false;
        }

        t = tmin;
        if(entryAxis == 0) normal = new Vector3(-Mathf.Sign(dir.x), 0, 0);
        else if(entryAxis == 1) normal = new Vector3(0, -Mathf.Sign(dir.y), 0);
        else if(entryAxis == 2) normal = new Vector3(0, 0, -Mathf.Sign(dir.z));
        return true;
    }
}

