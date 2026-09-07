using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Host of a block's container system: named data containers (persisted state)
// plus work containers (logic). Assembled by BlockEntityDefinition.CreateNewBlockEntity.
public class BlockEntity
{
    public BlockEntityDefinition Definition;
    public Vector3Int Position;   // dimension coordinates
    public Chunk OwnerChunk;      // injected by Chunk.RegisterBlockEntity; drives MarkDirty

    public readonly Dictionary<string, DataContainer> DataContainers = new();
    public readonly List<WorkContainer> WorkContainers = new();

    public void AddDataContainer(string name, DataContainer container)
    {
        container.Name = name;
        container.Host = this;
        if (DataContainers.ContainsKey(name))
            Debug.LogWarning($"[BlockEntity] duplicate data container '{name}', replaced");
        DataContainers[name] = container;
    }

    public T GetDataContainer<T>(string name) where T : DataContainer
        => DataContainers.TryGetValue(name, out var container) ? container as T : null;

    public void AddWorkContainer(WorkContainer container)
    {
        container.Host = this;
        WorkContainers.Add(container);
    }

    // Resolves every work container's data references by name; called once after assembly.
    public void BindWorkContainers()
    {
        foreach (var wc in WorkContainers) wc.OnBind(this);
    }

    public void TickWorkContainers()
    {
        foreach (var wc in WorkContainers) wc.Tick();
    }

    // Container state changed: the owning chunk's content now differs from
    // disk and must be saved (the same dirty flag player edits set).
    public void MarkDirty() => OwnerChunk?.MarkModifiedByBlockEntity();

    // Phase C: the panel session factory builds the mirror bindings and the
    // pure-view PanelModel on the logic side; the UI receives only the model
    // (rule R-C1-4: no BlockEntity reference reaches a panel).
    public void OnInteract(Entity entity, BlockEntityDefinition definition)
    {
        if(definition.UIFullName.Length == 0)return;
        var model = ContainerCommandProcessor.Instance.OpenPanel(this);
        UIManager.Instance.OpenUI(definition.UIFullName, model);
    }
}

// BE type - ResourceType. Carries the per-BE container configs and assembles
// fresh BlockEntity instances: data containers first (so OnBind references
// resolve), then work containers, then one OnBind pass.
public class BlockEntityDefinition : ResourceType
{
    public List<DataContainerConfig> DataContainers;
    public List<WorkContainerConfig> WorkContainers;
    public string UIFullName;

    public BlockEntity CreateNewBlockEntity(Vector3Int position)
    {
        var be = new BlockEntity { Position = position, Definition = this };
        if (DataContainers != null)
        {
            foreach (var cfg in DataContainers)
            {
                if (!ResourceSystem.Instance.DataContainerDefinitions.TryGetResourceWithFullName(cfg.TypeFullname, out var def))
                {
                    Debug.LogWarning($"[BlockEntityDefinition] unknown data container '{cfg.TypeFullname}'");
                    continue;
                }
                be.AddDataContainer(cfg.Name, def.Factory(cfg));
            }
        }
        if (WorkContainers != null)
        {
            foreach (var cfg in WorkContainers)
            {
                if (!ResourceSystem.Instance.WorkContainerDefinitions.TryGetResourceWithFullName(cfg.TypeFullname, out var def))
                {
                    Debug.LogWarning($"[BlockEntityDefinition] unknown work container '{cfg.TypeFullname}'");
                    continue;
                }
                be.AddWorkContainer(def.Factory(cfg));
            }
        }
        be.BindWorkContainers();
        return be;
    }
}
