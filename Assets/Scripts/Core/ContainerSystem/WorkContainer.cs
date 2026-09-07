using System.Collections.Generic;

public abstract class WorkContainer
{
    public BlockEntity Host;
    public virtual void OnBind(BlockEntity blockEntity){}
    public virtual void Tick(){}
    public virtual void OnRemoved(){}
    public virtual string Serialize() => "{}";
    public virtual void Deserialize(string json){}
    protected void MarkDirty() => Host?.MarkDirty();

    protected T GetData<T>(string name) where T:DataContainer
        => Host.GetDataContainer<T>(name);
}

public class WorkContainerConfig
{
    public string TypeFullname;
    public List<string> SupportedRecipeTypes;   // recipeType (parser full name) set this container runs; registration-time only
    public string Parameters;
}

// Work container type - ResourceType; factory creates the instance from its config.
public class WorkContainerDefinition : ResourceType
{
    public System.Func<WorkContainerConfig, WorkContainer> Factory;
}