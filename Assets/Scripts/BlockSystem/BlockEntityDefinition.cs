using System.Collections.Generic;
using UnityEngine;

// How a block entity renders:
// StaticBlock uses the static block pipeline (the block's default state model
// is merged into the chunk mesh); EntityModel uses dynamic entity-model visuals.
public enum BlockEntityRenderMode
{
    StaticBlock,
    EntityModel
}

// BlockEntity definition - ResourceType. Declares the render mode plus the
// module parameter list; modules are instantiated per placement by resolving
// each ModuleDefinition.ModuleTypeFullName against the module type registry.
public class BlockEntityDefinition : ResourceType
{
    public BlockEntityRenderMode RenderMode = BlockEntityRenderMode.StaticBlock;
    // StaticBlock: the block full name whose default state renders this BE
    // (usually the host block itself; can point at another block).
    public string BlockId;
    // EntityModel: entity model full name (ResourceSystem.EntityModels).
    public string EntityModelId;
    // Module parameter list (embedded, per-definition); null = plain BE.
    public List<ModuleDefinition> Modules = null;

    // Called when the host block is placed (or a chunk loads): creates the host
    // BE, instantiates every module through its registered factory, then runs
    // the OnPlaced callbacks (reference resolution and caching).
    public BlockEntity CreateNewBlockEntity(Vector3Int position, ushort stateId)
    {
        var be = new BlockEntity(position, stateId, this);
        if (Modules != null)
        {
            foreach (var md in Modules)
            {
                if (!ResourceSystem.Instance.BlockEntityModuleDefinitions
                        .TryGetResourceWithFullName(md.ModuleTypeFullName, out var type))
                {
                    Debug.LogWarning($"[BlockEntityDefinition] unknown module type {md.ModuleTypeFullName} for {FullName}; module skipped");
                    continue;
                }
                be.AddModule(type.Factory(md));
            }
        }
        be.InitModules();
        return be;
    }
}
