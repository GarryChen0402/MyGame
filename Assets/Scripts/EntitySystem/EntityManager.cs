using System.Collections.Generic;

public class EntityManager
{
    private static EntityManager instance = new();
    public static EntityManager Instance => instance;
    public bool InUpdating {get; private set;} = false;
    public readonly HashSet<Entity> entities = new();
    public readonly List<Entity> pendingRemoveList = new();
    public readonly List<Entity> pendingAddList = new();

    private EntityManager()
    {
        EventBus.Instance.Subscribe<SummonEntity>((evt)=> Register(evt.entity));
    }

    public void Register(Entity entity)
    {
        if(entity == null)return;
        if(!InUpdating)entities.Add(entity);
        else pendingAddList.Add(entity);
    }

    public void Unregister(Entity entity)
    {
        if(entity == null)return;
        if(!InUpdating)entities.Remove(entity);
        else pendingRemoveList.Add(entity);
    }

    public void Update(float dt)
    {
        InUpdating = true;
        foreach(var entity in entities)entity.OnUpdate(dt);
        InUpdating = false;

        foreach(var e in pendingAddList)Register(e);
        foreach(var e in pendingRemoveList)Unregister(e);
    }
}