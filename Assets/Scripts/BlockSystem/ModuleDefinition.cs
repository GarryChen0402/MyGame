using System.Collections.Generic;

// Who may perform an operation on an inventory: the player (UI), a module
// (internal logic) or both; None forbids the operation entirely.
public enum InventoryAccess { Any, Player, Module, None }

// Module parameters - plain data (not a ResourceType), embedded in
// BlockEntityDefinition.Modules. Each module type reads the fields it needs.
public class ModuleDefinition
{
    public string ModuleTypeFullName;   // references the BlockEntityModuleDefinitions registry
    public string Name;                 // module identifier, referenced by sibling modules; empty = anonymous
    public int Capacity = 27;           // inventory: slot count
    public InventoryAccess InsertPolicy = InventoryAccess.Any;   // inventory: who may insert
    public InventoryAccess ExtractPolicy = InventoryAccess.Any;  // inventory: who may extract
    public List<string> AllowedItems;   // inventory: allowed item full names; empty = unrestricted
    public List<string> AllowedTags;    // inventory: allowed item tags; empty = unrestricted
    public int GridWidth = 3;           // crafting: grid width (must match input inventory capacity)
    public int GridHeight = 3;          // crafting: grid height
    public string RecipeType;           // crafting/processing: recipe type full name
    public string InputInventory;       // crafting/processing: input inventory module name
    public string FuelInventory;        // processing: fuel inventory module name; null = no fuel logic
    public string OutputInventory;      // crafting/processing: output inventory module name
}
