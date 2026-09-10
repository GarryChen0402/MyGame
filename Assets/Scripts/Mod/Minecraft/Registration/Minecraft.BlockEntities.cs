using UnityEngine;

public partial class Minecraft
{
    // ---- L: containers + block entity definitions ----
    private void RegisterContainersAndBlockEntities()
    {
        // Container types (global once) + the furnace BE definition.
        ResourceSystem.Instance.DataContainerDefinitions.Register(new DataContainerDefinition
        { modId = "universal", name = "inventory", Factory = cfg => new InventoryDataContainer(cfg) });
        ResourceSystem.Instance.WorkContainerDefinitions.Register(new WorkContainerDefinition
        { modId = "universal", name = "processing", Factory = cfg => new ProcessingWorkContainer(cfg) });
        ResourceSystem.Instance.WorkContainerDefinitions.Register(new WorkContainerDefinition
        { modId = "universal", name = "crafting", Factory = cfg => new CraftingWorkContainer(cfg) });

        ResourceSystem.Instance.BlockEntityDefinitions.Register(new BlockEntityDefinition
        {
            modId = ModId,
            name = "furnace",
            UIFullName = FurnaceUI.furanceUIDefinition.FullName,
            DataContainers = new()
            {
                new DataContainerConfig
                {
                    Name = "input", TypeFullname = "universal:inventory",
                    Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config { Capacity = 1 })
                },
                new DataContainerConfig
                {
                    Name = "fuel", TypeFullname = "universal:inventory",
                    Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config
                    {
                        Capacity = 1,
                        AllowedTags = new() { "fuel" },
                        ExtractPolicy = ContainerAccess.Any
                    })
                },
                new DataContainerConfig
                {
                    Name = "output", TypeFullname = "universal:inventory",
                    Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config
                    {
                        Capacity = 1,
                        InsertPolicy = ContainerAccess.Module,
                        ExtractPolicy = ContainerAccess.Any
                    })
                }
            },
            WorkContainers = new()
            {
                new WorkContainerConfig
                {
                    TypeFullname = "universal:processing",
                    SupportedRecipeTypes = new() { "universal:processing" },
                    Parameters = JsonUtility.ToJson(new ProcessingWorkContainer.Config
                    {
                        Input = "input",
                        Fuel = "fuel",
                        Output = "output"
                    })
                }
            }
        });

        // Crafting table BE: 9-slot grid (freely insertable/extractable) +
        // 1-slot result (Module-insert only: players can only ever take the
        // preview out, never place into it) + the instant crafting logic.
        ResourceSystem.Instance.BlockEntityDefinitions.Register(new BlockEntityDefinition
        {
            modId = ModId,
            name = "crafting_table",
            UIFullName = CraftingTableUI.craftingTableUIDefinition.FullName,
            DataContainers = new()
            {
                new DataContainerConfig
                {
                    Name = "grid", TypeFullname = "universal:inventory",
                    Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config { Capacity = 9 })
                },
                new DataContainerConfig
                {
                    Name = "result", TypeFullname = "universal:inventory",
                    Parameters = JsonUtility.ToJson(new InventoryDataContainer.Config
                    {
                        Capacity = 1,
                        InsertPolicy = ContainerAccess.Module,
                        ExtractPolicy = ContainerAccess.Any
                    })
                }
            },
            WorkContainers = new()
            {
                new WorkContainerConfig
                {
                    TypeFullname = "universal:crafting",
                    SupportedRecipeTypes = new() { "universal:shaped", "universal:shapeless" },
                    Parameters = JsonUtility.ToJson(new CraftingWorkContainer.Config
                    {
                        Grid = "grid",
                        Result = "result"
                    })
                }
            }
        });
    }
}
