using System.Collections.Generic;
using UnityEngine;

public class Entity // Data Class
{
    public List<AABB> AABBs = new();
    public AABB MainBox => AABBs[0];
    
    public Vector3 Position => MainBox.Pivot;

    public float pitch = 0;
    public float yaw = 0;
    public ushort DimensionId;

    public Entity()
    {
        EventBus.Instance.Publish(new SummonEntity(){entity = this});
    }

    public void OnDestroy() => EventBus.Instance.Publish(new DestroyEntity(){entity = this});

    public void Move(Vector3 motion)
    {
        //TODO Use the Move logic like mc, get the MoveResult from PhysicsManager
        // for(int i = 0; i < AABBs.Count; i++)
        // {
        //     AABBs[i] = AABBs[i].ApplyMotion(motion);
        // }
        PhysicsManager.Instance.MoveEntity(this, motion);
    }
    
}