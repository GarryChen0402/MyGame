using System.Collections.Generic;
using UnityEngine;

public abstract class WorkContainer
{
    public BlockEntity Host;
    public virtual void OnBind(BlockEntity blockEntity){}
    public virtual void Tick(){}
    public virtual void OnRemoved(){}
    public virtual string Serialize() => "{}";
    public virtual void Deserialize(string json){}

    // Panel self-report (S2 of Docs/改造提案-UI数据流拆分方案.md): the container
    // declares what its panel shows - slot contributions (name + source +
    // accessor), channel contributions and close actions. Default contributes
    // nothing (a container without a UI).
    public virtual void DescribePanel(PanelBuildContext ctx){}

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

// One slot contribution of a panel: the container-declared slot code, the
// source container + cell it mirrors and the click accessor. The name IS the
// container's own data-side code (P2: the code table lives on the container,
// so the panel packet keys and the pack keys can never drift apart).
public class PanelSlotSource
{
    public string Name;
    public InventoryDataContainer Container;
    public int SourceIndex;
    public ISlotAccess Accessor;
}

// One channel contribution: Source fills Names.Length consecutive channels of
// the panel packet, starting at the head assigned by contribution order.
public class ChannelSource
{
    public string[] Names;
    public IChannelSource Source;
}

// Panel aggregation context: OpenPanel hands this to every work container of
// the block entity, then flattens the contributions into one PanelData packet.
// Names are the alignment key shared by data, layout description and UI
// handles (resolved in S3); indices stay contribution-ordered for now.
public class PanelBuildContext
{
    public readonly List<PanelSlotSource> SlotSources = new();
    public readonly List<ChannelSource> ChannelSources = new();

    // Close-time actions of the contributing containers, chained in
    // contribution order (e.g. the crafting preview clear).
    public System.Action OnClose;

    // One container cell, named by the container's own slot code (P2); a cell
    // without a declared code drops the contribution with a warning.
    public void AddSlot(InventoryDataContainer container, int sourceIndex, ISlotAccess accessor)
    {
        string code = container?.SlotCode(sourceIndex);
        if(code == null)
        {
            Debug.LogWarning($"[PanelBuildContext] container '{container?.Name}' has no slot code for cell {sourceIndex}; contribution skipped");
            return;
        }
        SlotSources.Add(new PanelSlotSource { Name = code, Container = container, SourceIndex = sourceIndex, Accessor = accessor });
    }

    // Grid-style bulk contribution: one consecutive source cell per declared
    // code.
    public void AddSlots(InventoryDataContainer container, int count, System.Func<int, ISlotAccess> accessorFactory)
    {
        for(int i = 0; i < count; i++) AddSlot(container, i, accessorFactory(i));
    }

    public void AddChannels(string[] names, IChannelSource source)
        => ChannelSources.Add(new ChannelSource { Names = names, Source = source });

    public void AddOnClose(System.Action action) => OnClose += action;

    public string[] SlotNames
    {
        get
        {
            var names = new string[SlotSources.Count];
            for(int i = 0; i < names.Length; i++)names[i] = SlotSources[i].Name;
            return names;
        }
    }

    public string[] ChannelNames
    {
        get
        {
            var names = new List<string>();
            foreach(var channel in ChannelSources)names.AddRange(channel.Names);
            return names.ToArray();
        }
    }
}