using System;

// Module type - ResourceType. One global registry entry per module *behavior*
// (inventory / processing / crafting / custom); the factory turns a
// ModuleDefinition (per-BE parameters) into a module instance.
public class BlockEntityModuleDefinition : ResourceType
{
    public Func<ModuleDefinition, BlockEntityModule> Factory;
}
