using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PhysicsManager
{
    private static readonly PhysicsManager instance = new();
    public static PhysicsManager Instance => instance;

    private readonly Dictionary<Entity, List<AABB>> entites = new();

    private PhysicsManager()
    {
        EventBus.Instance.Subscribe<SummonEntity>(OnSummonEntity);
        EventBus.Instance.Subscribe<DestroyEntity>(OnDestroyEntity);
    }

    private void Register(Entity entity)
    {
        if(entites.ContainsKey(entity))return;
        entites[entity] = entity.AABBs;
    }

    private void UnRegister(Entity entity)
        => entites.Remove(entity);

    private void OnSummonEntity(SummonEntity evt)
        => Register(evt.entity);
    
    private void OnDestroyEntity(DestroyEntity evt)
        => UnRegister(evt.entity);

    public List<List<AABB>> GetAllCollisionBoxes() => entites.Values.ToList();

    // MC-style axis-separated movement: resolve Y first, then Z, then X.
    // Each axis is swept against solid blocks and truncated at the first contact.
    public MoveResult MoveEntity(Entity entity, Vector3 motion)
    {
        if(!entites.TryGetValue(entity, out List<AABB> boxes)) return default;

        float dx = motion.x, dy = motion.y, dz = motion.z;
        bool onGround = false, hitCeiling = false, hitWall = false;

        dy = MoveAxis(entity, boxes, dy, Axis.Y, ref onGround, ref hitCeiling);
        ApplyToAll(boxes, new Vector3(0, dy, 0));
        dz = MoveAxis(entity, boxes, dz, Axis.Z, ref hitWall);
        ApplyToAll(boxes, new Vector3(0, 0, dz));
        dx = MoveAxis(entity, boxes, dx, Axis.X, ref hitWall);
        ApplyToAll(boxes, new Vector3(dx, 0, 0));

        return new MoveResult
        (
            new Vector3(dx, dy, dz),
            onGround,
            hitCeiling,
            hitWall,
            onGround || hitCeiling || hitWall
        );
    }

    // Sweep one axis: expand each box by Eps, move it along the axis, then test
    // every solid block inside the swept area. minT = earliest contact fraction.
    private float MoveAxis(Entity entity, List<AABB> boxes, float amount, Axis axis, ref bool negativeFlag, ref bool positiveFlag)
    {
        if(amount == 0) return 0;
        if(!WorldManager.Instance.TryGetDimension(entity.DimensionId, out Dimension dim)) return amount;

        float minT = 1f;
        Vector3 axisVec = axis switch
        {
            Axis.X => Vector3.right,
            Axis.Y => Vector3.up,
            _ => Vector3.forward
        };
        foreach(AABB box in boxes)
        {
            AABB expanded = new(box.MinRange - Vector3.one * Eps, box.MaxRange + Vector3.one * Eps);
            AABB swept = expanded.ApplyMotion(axisVec * amount);
            for(int x = Mathf.FloorToInt(swept.MinRange.x); x <= Mathf.FloorToInt(swept.MaxRange.x - Eps); x++)
            for(int y = Mathf.FloorToInt(swept.MinRange.y); y <= Mathf.FloorToInt(swept.MaxRange.y - Eps); y++)
            for(int z = Mathf.FloorToInt(swept.MinRange.z); z <= Mathf.FloorToInt(swept.MaxRange.z - Eps); z++)
            {
                ForEachBlockCollisionBox(dim, new Vector3Int(x, y, z), blockBox =>
                {
                    float t = CollideAxis(box, blockBox, amount, axis);
                    if(t < minT) minT = t;
                });
            }
        }

        if(minT < 1f)
        {
            if(axis == Axis.Y)
            {
                if(amount < 0) negativeFlag = true;
                else positiveFlag = true;
            }
            else
            {
                negativeFlag = true;
            }
        }
        return amount * minT;
    }

    private float MoveAxis(Entity entity, List<AABB> boxes, float amount, Axis axis, ref bool hitWall)
        => MoveAxis(entity, boxes, amount, axis, ref hitWall, ref hitWall);

    // Earliest contact fraction [0,1] along one axis; the other two axes must overlap.
    private static float CollideAxis(AABB entityBox, AABB blockBox, float amount, Axis axis)
    {
        bool overlaps = axis switch
        {
            Axis.X => entityBox.MinRange.y < blockBox.MaxRange.y && entityBox.MaxRange.y > blockBox.MinRange.y 
                && entityBox.MinRange.z < blockBox.MaxRange.z && entityBox.MaxRange.z > blockBox.MinRange.z,
            Axis.Y => entityBox.MinRange.x < blockBox.MaxRange.x && entityBox.MaxRange.x > blockBox.MinRange.x 
                && entityBox.MinRange.z < blockBox.MaxRange.z && entityBox.MaxRange.z > blockBox.MinRange.z,
            _ => entityBox.MinRange.x < blockBox.MaxRange.x && entityBox.MaxRange.x > blockBox.MinRange.x 
                && entityBox.MinRange.y < blockBox.MaxRange.y && entityBox.MaxRange.y > blockBox.MinRange.y,
        };
        if(!overlaps) return 1f;

        float t = axis switch
        {
            Axis.X => amount > 0 ? (blockBox.MinRange.x - entityBox.MaxRange.x) / amount
                                : (blockBox.MaxRange.x - entityBox.MinRange.x) / amount,
            Axis.Y => amount > 0 ? (blockBox.MinRange.y - entityBox.MaxRange.y) / amount
                                : (blockBox.MaxRange.y - entityBox.MinRange.y) / amount,
            _ => amount > 0 ? (blockBox.MinRange.z - entityBox.MaxRange.z) / amount
                            : (blockBox.MaxRange.z - entityBox.MinRange.z) / amount,
        };
        return Mathf.Clamp01(t);
    }

    // Enumerate the world-space collision boxes of the block at coord, taken from
    // its BlockDefinition.AABBs (block-space, relative to the block origin).
    // Falls back to a full cube when the definition has no custom boxes.
    public static void ForEachBlockCollisionBox(Dimension dim, Vector3Int coord, System.Action<AABB> onBox)
    {
        ushort blockId = dim.GetBlockAt(coord);
        if(blockId == 0) return;

        if (!ResourceSystem.Instance.BlockDefinitions.TryGetResourceWithNumberId(blockId, out BlockDefinition def) || def.AABBs == null || def.AABBs.Count == 0)
        {
            Vector3 origin = new(coord.x, coord.y, coord.z);
            onBox(new AABB(origin, origin + Vector3.one));
            return;
        }

        Vector3 blockOrigin = new(coord.x, coord.y, coord.z);
        foreach(AABB localBox in def.AABBs)
            onBox(new AABB(blockOrigin + localBox.MinRange, blockOrigin + localBox.MaxRange));
    }

    private static void ApplyToAll(List<AABB> boxes, Vector3 motion)
    {
        for(int i = 0; i < boxes.Count; i++)
            boxes[i] = boxes[i].ApplyMotion(motion);
    }

    private const float Eps = 1e-4f;

    private enum Axis { X, Y, Z }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInitialized() => _= Instance;
}