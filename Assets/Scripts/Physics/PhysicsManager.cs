using System.Collections.Generic;
using UnityEngine;

public class PhysicsManager
{
    public static PhysicsManager Instance { get; } = new();

    private const float Epsilon = 1e-4f;

    private PhysicsManager()
    {
        EventBus.Instance.Subscribe<SummonEntity>(OnSummonEntity);
        EventBus.Instance.Subscribe<DestroyEntity>(OnDestroyEntity);
    }

    public void Destroy()
    {
        EventBus.Instance.Unsubscribe<DestroyEntity>(OnDestroyEntity);
        EventBus.Instance.Unsubscribe<SummonEntity>(OnSummonEntity);
    }

    private readonly Dictionary<Entity, List<AABB>> entityBoxes = new();

    public void RegisterEntity(Entity entity)
    {
        if(entityBoxes.ContainsKey(entity))return;
        entityBoxes.Add(entity, entity.CollisionBox);
    }

    public void UnregisterEntity(Entity entity)
    {
        entityBoxes.Remove(entity);
    }

    public bool TryGetBoxes(Entity entity, out List<AABB> boxes)
    {
        return entityBoxes.TryGetValue(entity, out boxes);
    }

    private void OnSummonEntity(SummonEntity evt)
        => RegisterEntity(evt.entity);
    
    private void OnDestroyEntity(DestroyEntity evt)
        => UnregisterEntity(evt.entity);
    

    // Minecraft-style axis-separated collision: resolve Y first (vertical settles
    // before horizontal), then Z and X, truncating each axis against solid blocks.
    public MoveResult MoveEntity(Entity entity, Vector3 motion)
    {
        if(!WorldManager.Instance.TryGetDimension(entity.DimensionId, out var dim))
        {
            entity.Move(motion);
            return new MoveResult(motion, false, false, false, false);
        }

        float tY = MoveAlongAxis(entity, dim, 1, motion.y);
        bool onGround = tY < 1f && motion.y < 0f;
        bool hitCeiling = tY < 1f && motion.y > 0f;
        motion.y *= tY;
        entity.Move(new Vector3(0f, motion.y, 0f));

        float tZ = MoveAlongAxis(entity, dim, 2, motion.z);
        motion.z *= tZ;
        entity.Move(new Vector3(0f, 0f, motion.z));

        float tX = MoveAlongAxis(entity, dim, 0, motion.x);
        motion.x *= tX;
        entity.Move(new Vector3(motion.x, 0f, 0f));

        bool collided = tY < 1f || tZ < 1f || tX < 1f;
        return new MoveResult(motion, onGround, hitCeiling, tZ < 1f || tX < 1f, collided);
    }

    // Truncates `amount` on `axis` to the earliest collision along the swept path
    // and returns the free fraction [0, 1]. The entity must already be positioned
    // at its resolved location for the previous axes.
    private float MoveAlongAxis(Entity entity, Dimension dim, int axis, float amount)
    {
        if (amount == 0f) return 1f;

        Vector3 axisVec = axis switch { 0 => Vector3.right, 1 => Vector3.up, _ => Vector3.forward };
        AABB box = entity.MainBox.Expand(Epsilon);
        AABB moved = box.Move(axisVec * amount);

        // Swept volume covers both endpoints; subtract epsilon so a surface the
        // box exactly rests on is not re-enumerated as an overlapping block.
        int x0 = Mathf.FloorToInt(Mathf.Min(box.minX, moved.minX));
        int y0 = Mathf.FloorToInt(Mathf.Min(box.minY, moved.minY));
        int z0 = Mathf.FloorToInt(Mathf.Min(box.minZ, moved.minZ));
        int x1 = Mathf.FloorToInt(Mathf.Max(box.maxX, moved.maxX) - Epsilon);
        int y1 = Mathf.FloorToInt(Mathf.Max(box.maxY, moved.maxY) - Epsilon);
        int z1 = Mathf.FloorToInt(Mathf.Max(box.maxZ, moved.maxZ) - Epsilon);

        float minT = 1f;
        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
        {
            if (dim.GetBlockAt(new Vector3Int(x, y, z)) == 0) continue;
            AABB blockBox = new AABB(x, y, z, x + 1, y + 1, z + 1);
            float t = ContactFraction(box, blockBox, axis, amount);
            if (t < minT) minT = t;
        }
        return minT;
    }

    // Earliest contact fraction [0, 1] between the entity box and a block box
    // along one axis; returns 1 when the block is off the swept path.
    private static float ContactFraction(AABB box, AABB block, int axis, float amount)
    {
        float t;
        switch (axis)
        {
            case 0: // X
                if (box.maxY <= block.minY || box.minY >= block.maxY ||
                    box.maxZ <= block.minZ || box.minZ >= block.maxZ) return 1f;
                t = amount > 0f ? (block.minX - box.maxX) / amount : (block.maxX - box.minX) / amount;
                break;
            case 1: // Y
                if (box.maxX <= block.minX || box.minX >= block.maxX ||
                    box.maxZ <= block.minZ || box.minZ >= block.maxZ) return 1f;
                t = amount > 0f ? (block.minY - box.maxY) / amount : (block.maxY - box.minY) / amount;
                break;
            default: // Z
                if (box.maxX <= block.minX || box.minX >= block.maxX ||
                    box.maxY <= block.minY || box.minY >= block.maxY) return 1f;
                t = amount > 0f ? (block.minZ - box.maxZ) / amount : (block.maxZ - box.minZ) / amount;
                break;
        }
        return Mathf.Clamp01(t);
    }
}
