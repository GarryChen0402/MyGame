using UnityEngine;

public struct AABB
{
    public float minX;
    public float minY;
    public float minZ;
    public float maxX;
    public float maxY;
    public float maxZ;

    public AABB(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
    {
        this.minX = minX;
        this.minY = minY;
        this.minZ = minZ;
        this.maxX = maxX;
        this.maxY = maxY;
        this.maxZ = maxZ;
    }

    public bool Intersects(AABB other)
    {
        return minX < other.maxX && maxX > other.minX
            && minY < other.maxY && maxY > other.minY
            && minZ < other.maxZ && maxZ > other.minZ;
    }

    public readonly Vector3 Pivot => new((minX + maxX) / 2, minY, (minZ + maxZ) / 2);

    // Returns a new box expanded by amount on every side.
    public AABB Expand(float amount)
    {
        return new AABB(
            minX - amount, minY - amount, minZ - amount,
            maxX + amount, maxY + amount, maxZ + amount);
    }

    // Returns a new box translated by motion; structs are value types, so mutating
    // in place through a property/index would silently modify a copy.
    public AABB Move(Vector3 motion)
    {
        return new AABB(
            minX + motion.x, minY + motion.y, minZ + motion.z,
            maxX + motion.x, maxY + motion.y, maxZ + motion.z);
    }
}