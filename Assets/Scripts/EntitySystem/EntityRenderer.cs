using UnityEngine;

// Render shell for one dynamic entity: one GameObject per entity, reading
// entity data one-way every frame (data/logic live in the owning chunk and
// ItemEntityManager - design doc Docs/掉落物ItemEntity实现方案.md §7). Never
// writes back to the entity; the GO transform is purely an output.
public class EntityRenderer : MonoBehaviour
{
    private ItemEntity entity;
    private float spinAngle;   // accumulated, frame-rate independent

    // Spin speed in deg/s; vanilla item drops rotate slowly around Y.
    private const float SpinSpeed = 90f;

    public void Bind(ItemEntity entity)
    {
        this.entity = entity;
        if(entity == null)return;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(entity.Stack.itemId, out var def))return;
        Mesh mesh = def.IsBlockItem
            ? ItemMeshLibrary.GetOrCreateBlockMesh(entity.Stack.itemId, def)
            : ItemMeshLibrary.GetOrCreateItemMesh(entity.Stack.itemId, def);
        if(mesh == null)return;

        // Mesh spans a 1m block centered on the origin; the manager scaled the
        // shell to 0.25, matching the entity's 0.25 physics box.
        var filter = gameObject.GetComponent<MeshFilter>();
        if(filter == null)filter = gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var mr = gameObject.GetComponent<MeshRenderer>();
        if(mr == null)mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ItemIconRenderSystem.SharedIconMaterial;
    }

    private void Update()
    {
        if(entity == null)return;
        // Bob phase from LifeTime (design doc §7): sin(Lifetime * 2f) -> ~3.1s
        // period, 0.06 amplitude. Position aligns the model with the physics
        // box: mesh center maps to the box center, not the feet pivot.
        Vector3 boxCenter = (entity.MainBox.MinRange + entity.MainBox.MaxRange) * 0.5f;
        float bob = Mathf.Sin(entity.LifeTime * 2f) * 0.06f;
        transform.localPosition = boxCenter + Vector3.up * bob;
        spinAngle += Time.deltaTime * SpinSpeed;
        transform.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
    }
}
