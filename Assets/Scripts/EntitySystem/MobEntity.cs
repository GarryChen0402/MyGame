using UnityEngine;
public class MobEntity : Entity
{
    public MobDefinition Definition {get; private set;} = null;

    public MobEntity()
    {
        EntityManager.Instance.Register(this);
    }

    public void Init(MobDefinition def)
    {
        Definition = def;
        AABBs.Clear();
        foreach(var box in def.CollisionBoxes)AABBs.Add(box);
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
    }
}