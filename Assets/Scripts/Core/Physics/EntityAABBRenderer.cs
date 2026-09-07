using System.Collections.Generic;
using UnityEngine;

public class EntityAABBRenderer : MonoBehaviour
{
    private List<List<AABB>> allEntityAABBs = new();
    private void Awake()
    {
        EventBus.Instance.Subscribe<SummonEntityEvent>(OnSummonEntity);
        EventBus.Instance.Subscribe<DestroyEntity>(OnDestroyEntity);
        RefreshAABBLists();
    }

    private void OnSummonEntity(SummonEntityEvent evt)
    {
        RefreshAABBLists();
    }

    private void OnDestroyEntity(DestroyEntity evt)
    {
        RefreshAABBLists();
    }

    private void RefreshAABBLists()
    {
        allEntityAABBs = PhysicsManager.Instance.GetAllCollisionBoxes();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (List<AABB> entityAABBs in allEntityAABBs)
        {
            foreach (AABB aabb in entityAABBs)
            {
                Vector3 center = (aabb.MinRange + aabb.MaxRange) * 0.5f;
                Vector3 size = aabb.MaxRange - aabb.MinRange;
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}