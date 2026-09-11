public abstract class DataContainer
{
    public string Name;
    public BlockEntity Host;

    public virtual string Serialize() => "{}";
    public virtual void Deserialize(string json) {}

    // Container-level pack (P2, design §2.7): the runtime content flattened
    // into a keyed string ("code=payload" entries) for the mirror layer; the
    // UI-side group unpacks it. Null = this container takes no part in the
    // pack path.
    public virtual string GetPackData() => null;

    protected void MarkDirty() => Host?.MarkDirty();
}

public class DataContainerConfig
{
    public string Name;              // unique within the BE
    public string TypeFullname;      // references the DataContainerDefinitions registry
    public string Parameters;        // JSON string, parsed by the data container subclass
}

// Data container type - ResourceType; factory creates the instance from its config.
public class DataContainerDefinition : ResourceType
{
    public System.Func<DataContainerConfig, DataContainer> Factory;
}