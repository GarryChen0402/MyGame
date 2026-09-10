using System.Collections.Generic;
using UnityEngine;

public partial class Minecraft : IMod, ISteppedModRegistration
{
    public static readonly string ModId = "Minecraft".ToLower();

    string IMod.ModId => ModId;
    public int LoadPriority => 0;

    // Registration state hoisted to fields (Part B §5.3 split discipline:
    // segment boundaries may not slice statements, so state crossing a
    // boundary moves to the instance). The 19 segments A-S (doc §2.3) are the
    // natural boundaries of the former monolithic RegisterAllResources -
    // statements are neither reordered nor split, only wrapped in methods.
    // Each segment now lives in a partial file under Registration/; the
    // RegisterStep order below is unchanged and still fixes id assignment.
    private CustomModel cube;
    private CustomModel stairModel;
    private List<string> allIds;
    private BlockDefinition air;
    private BlockDefinition stoneDefinition;
    private BlockDefinition dirtDefinition;
    private BlockDefinition grassDefinition;
    private BlockDefinition stairDefinition;
    private BlockDefinition cobblestoneDefinition;
    private BlockDefinition furnaceDefinition;
    private BlockDefinition craftingTableDefinition;

    // Cached block ids used by biome fill columns.
    private static ushort grassId, dirtId, stoneId;

    // Default state ids for the grass random-tick spread, resolved lazily on
    // first use: GetDefaultState needs the PostFreeze state table, which does
    // not exist during registration (sentinel ushort.MaxValue = unresolved).
    private static ushort grassDefaultStateId = ushort.MaxValue;
    private static ushort dirtDefaultStateId = ushort.MaxValue;

    private static DimensionDefinition testDi = new()
    {
        modId = ModId,
        name = "test_dim",
        MinSubChunkIndex = -4,
        // Biome surfaces reach up to ~106 (mountains 92 + scale 14); the chunk
        // range must cover them or the world renders as a flat cap at y=32.
        MaxSubChunkIndex = 7,
        // Content-declared start dimension for fresh worlds (design §A.4.7).
        IsStartDimension = true,
        DimensionGeneratorName = $"{ModId}:biome_dim_generator"
    };

    public int StepCount => 19;

    public void RegisterStep(int stepIndex)
    {
        switch(stepIndex)
        {
            case 0: BuildCustomModels(); break;                     // A: custom models
            case 1: RegisterTextures(); break;                      // B: textures
            case 2: RegisterEntityAssets(); break;                  // C: entity model + animations
            case 3: BuildBlockDefinitions(); break;                 // D: block definitions
            case 4: RegisterBlocks(); break;                        // E: block registration + id readback
            case 5: RegisterDimensions(); break;                    // F: dimension generators + definition
            case 6: RegisterBiomes(); break;                        // G: biomes
            case 7: RegisterItemBehaviors(); break;                 // H: item behaviors
            case 8: RegisterItems(); break;                         // I: items
            case 9: RegisterRecipeParsers(); break;                 // J: recipe parsers
            case 10: RegisterRecipeTexts(); break;                  // K: recipe texts
            case 11: RegisterContainersAndBlockEntities(); break;   // L: containers + BE definitions
            case 12: RegisterUisAndInputHandlers(); break;          // M: UI definitions + input handlers
            case 13: RegisterKeyBindings(); break;                  // N: key bindings
            case 14: RegisterMobs(); break;                         // O: mob model + definition
            case 15: RegisterBuffs(); break;                        // P: buff types
            case 16: RegisterAi(); break;                           // Q: AI spec
            case 17: RegisterCrackTextures(); break;                // R: crack overlay textures
            case 18: RegisterLootTables(); break;                   // S: loot tables
        }
    }

    // Full registration = the same 19 segments back to back (IMod contract;
    // the bootstrapper drives RegisterStep across frames instead).
    public void RegisterAllResources()
    {
        for(int i = 0; i < StepCount; i++)RegisterStep(i);
    }
}
