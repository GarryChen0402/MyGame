using UnityEngine;
public class MobEntity : Entity
{
    public MobDefinition Definition {get; private set;} = null;

    // Visual shell of the mob's EntityModel, built on Init and re-aligned to
    // the collision boxes every frame (entities have no own transform).
    public EntityVisual Visual {get; private set;}

    public MobEntity()
    {
        EntityManager.Instance.Register(this);
    }

    public void Init(MobDefinition def)
    {
        Definition = def;
        AABBs.Clear();
        foreach(var box in def.CollisionBoxes)AABBs.Add(box);
        BuildVisual();
    }

    public void Init(MobDefinition def, Vector3 pos)
    {
        Definition = def;
        AABBs.Clear();
        foreach(var box in def.CollisionBoxes)
        {
            AABBs.Add( new AABB()
            {
                MinRange = box.MinRange + pos,
                MaxRange = box.MaxRange + pos
            });

        }
        BuildVisual();
    }

    // Resolves Definition.ModelId against the EntityModels registry and builds
    // the visual under the world root. Missing id / missing source leaves
    // Visual null (logged by the builder); callers tolerate that.
    private void BuildVisual()
    {
        if(Definition == null || string.IsNullOrEmpty(Definition.ModelId))return;
        if(!ResourceSystem.Instance.EntityModels.TryGetResourceWithFullName(Definition.ModelId, out var model))return;
        Visual = EntityVisualBuilder.Build(model, null, null, null);
        if(Visual == null)return;
        Visual.Root.position = MainBox.Pivot;   // box feet anchor, before the first render
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
        if(Visual?.Root != null && AABBs.Count > 0)
            Visual.Root.position = MainBox.Pivot;
    }
}