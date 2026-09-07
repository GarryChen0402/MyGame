using UnityEngine;

// Render shell for one dynamic entity: one GameObject per entity, reading the
// entity mirror one-way every frame (rule R-C2-2 - the logic entity lives in
// the owning chunk and ItemEntityManager; design doc 掉落物ItemEntity实现方案.md
// §7). Never writes back; the GO transform is purely an output. Bind is called
// by EntityRenderManager's spawn handler with the mirror and the item id the
// spawn DTO carried (mesh route selection) - no logic reference is held.
public class EntityRenderer : MonoBehaviour
{
    private EntityMirror mirror;
    private float spinAngle;   // accumulated, frame-rate independent
    private float birthTime;   // scene time at Bind; bob phase anchor (visual clock)

    // Spin speed in deg/s; vanilla item drops rotate slowly around Y.
    private const float SpinSpeed = 90f;

    public void Bind(EntityMirror mirror, ushort itemId)
    {
        this.mirror = mirror;
        birthTime = Time.time;
        if(mirror == null)return;
        if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(itemId, out var def))return;
        Mesh mesh = def.IsBlockItem
            ? ItemMeshLibrary.GetOrCreateBlockMesh(itemId, def)
            : ItemMeshLibrary.GetOrCreateItemMesh(itemId, def);
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
        if(mirror == null)return;
        // Partial-tick lerp of the tick states (design doc 固定Tick时钟与渲染
        // 插值改造-代码设计.md §4): the box center = interpolated feet pivot +
        // half of the mirrored box height (the box only translates, so prev and
        // current heights match). Bob phase runs on the frame clock (Time.time
        // - birthTime keeps the old Lifetime-phase continuous): pure visuals
        // never read logic fields.
        Vector3 pivot = Vector3.Lerp(mirror.PrevPosition, mirror.Position, GameClock.Alpha);
        float halfHeight = (mirror.BoxMaxY - mirror.BoxMinY) * 0.5f;
        float bob = Mathf.Sin((Time.time - birthTime) * 2f) * 0.06f;   // ~3.1s period, 0.06 amplitude
        transform.localPosition = pivot + Vector3.up * (halfHeight + bob);
        spinAngle += Time.deltaTime * SpinSpeed;
        transform.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
    }
}
