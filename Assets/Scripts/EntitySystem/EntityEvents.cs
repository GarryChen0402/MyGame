

public class SummonEntityEvent : GameEvent
{
    public Entity entity;
}

public class DestroyEntity : GameEvent
{
    public Entity entity;
}

public class HurtEntity : GameEvent
{
    public Entity entity;
    public float amount;
}

public class DeathEntity : GameEvent
{
    public Entity entity;
}