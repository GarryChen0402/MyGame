using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceSystem
{
    public static ResourceSystem Instance {get; } = new ResourceSystem();
    // Model Info
    public ResourceRegistryTable<CustomModel> CustomModels {get;} = new();
    // Block Definition
    public ResourceRegistryTable<BlockDefinition> BlockDefinitions {get;} = new();
    // Dimension Definiton 
    public ResourceRegistryTable<DimensionDefinition> DimensionDefinitions {get;} = new();
    public ResourceRegistryTable<DimensionGeneratorResource> DimensionGenerator {get;} = new();


    // Texture 
    public ResourceRegistryTable<TextureResource> Textures {get; } = new();
    private Texture2D blockAtlas;

    // BlockMaterial
    public Material BlockMaterial {get;} = new Material(Shader.Find("Universal Render Pipeline/Lit"));

    public bool RegisterTexture(string modId, string name, Texture2D source)
        => Textures.Register(new TextureResource
        {
            modId = modId,
            name = name,
            Atlas = source
        });

    // Lock all resource tables. Called by GameBootstrap after mod registration:
    // run-time registration is a boot bug and would corrupt atlas UVs / lookups.
    public void Freeze()
    {
        CustomModels.Freeze();
        BlockDefinitions.Freeze();
        DimensionDefinitions.Freeze();
        DimensionGenerator.Freeze();
        Textures.Freeze();
    }
    
    public void BuildAtlas()
    {
        var all = new List<Texture2D>();
        foreach(var tex in Textures.Values)all.Add(tex.Atlas);
        Texture2D packer = new(2, 2);

        Rect[] rects = packer.PackTextures(all.ToArray(), 0, 4096);
        int i=0;
        foreach(var tex in Textures.Values)tex.AtlasUVRect = rects[i++];

        packer.filterMode = FilterMode.Point;
        packer.wrapMode = TextureWrapMode.Clamp;

        blockAtlas = packer;
        BlockMaterial.SetTexture("_BaseMap", blockAtlas);
        // Object.DontDestroyOnLoad(blockAtlas);
    }
}