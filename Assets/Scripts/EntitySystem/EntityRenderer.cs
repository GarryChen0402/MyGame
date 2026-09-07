using UnityEngine;

// Render shell for one dynamic entity: one GameObject per entity, reading
// entity data one-way every frame (data/logic live in the owning chunk and
// ItemEntityManager - design doc Docs/掉落物ItemEntity实现方案.md §7). Never
// writes back to the entity; the GO transform is purely an output.
public class EntityRenderer : MonoBehaviour
{
    private ItemEntity entity;
    private float spinAngle;   // accumulated, frame-rate independent
    private float birthTime;   // scene time at Bind; bob phase anchor (visual clock)

    // Spin speed in deg/s; vanilla item drops rotate slowly around Y.
    private const float SpinSpeed = 90f;

    public void Bind(ItemEntity entity)
    {
        this.entity = entity;
        birthTime = Time.time;
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
        // Partial-tick lerp of the tick states (design doc 固定Tick时钟与渲染
        // 插值改造-代码设计.md §4): the box center = interpolated feet pivot +
        // half of the current box height (the box only translates, so prev and
        // current heights match). Bob phase runs on the frame clock (Time.time
        // - birthTime keeps the old Lifetime-phase continuous): pure visuals
        // never read 20Hz logic fields.
        Vector3 pivot = Vector3.Lerp(entity.PrevPosition, entity.Position, GameClock.Alpha);
        float halfHeight = (entity.MainBox.MaxRange.y - entity.MainBox.MinRange.y) * 0.5f;
        float bob = Mathf.Sin((Time.time - birthTime) * 2f) * 0.06f;   // ~3.1s period, 0.06 amplitude
        transform.localPosition = pivot + Vector3.up * (halfHeight + bob);
        spinAngle += Time.deltaTime * SpinSpeed;
        transform.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
    }
}
