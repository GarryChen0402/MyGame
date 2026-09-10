using UnityEngine;

public partial class Minecraft
{
    // ---- B: block / item / entity textures ----
    private void RegisterTextures()
    {
        // Texture Content
        ResourceSystem.Instance.RegisterTexture(ModId, "stone", Resources.Load<Texture2D>("Textures/Blocks/stone"));
        ResourceSystem.Instance.RegisterTexture(ModId, "dirt", Resources.Load<Texture2D>("Textures/Blocks/dirt"));
        ResourceSystem.Instance.RegisterTexture(ModId, "grass", Resources.Load<Texture2D>("Textures/Blocks/grass"));
        ResourceSystem.Instance.RegisterTexture(ModId, "grass_side", Resources.Load<Texture2D>("Textures/Blocks/grass_side"));
        ResourceSystem.Instance.RegisterTexture(ModId, "diamond_sword", Resources.Load<Texture2D>("Textures/Items/diamond_sword"));
        ResourceSystem.Instance.RegisterTexture(ModId, "firefly", Resources.Load<Texture2D>("Textures/Entities/firefly"));
        // Furnace textures use the "furance" spelling on disk.
        ResourceSystem.Instance.RegisterTexture(ModId, "cobblestone", Resources.Load<Texture2D>("Textures/Blocks/cobblestone"));
        ResourceSystem.Instance.RegisterTexture(ModId, "furance_front_nowork", Resources.Load<Texture2D>("Textures/Blocks/furance_front_nowork"));
        ResourceSystem.Instance.RegisterTexture(ModId, "furance_front_working", Resources.Load<Texture2D>("Textures/Blocks/furance_front_working"));
        ResourceSystem.Instance.RegisterTexture(ModId, "furance_side", Resources.Load<Texture2D>("Textures/Blocks/furance_side"));
        ResourceSystem.Instance.RegisterTexture(ModId, "furance_top_bottom", Resources.Load<Texture2D>("Textures/Blocks/furance_top_bottom"));
        ResourceSystem.Instance.RegisterTexture(ModId, "coal", Resources.Load<Texture2D>("Textures/Items/coal"));
        ResourceSystem.Instance.RegisterTexture(ModId, "crafting_table_front", Resources.Load<Texture2D>("Textures/Blocks/crafting_table_front"));
        ResourceSystem.Instance.RegisterTexture(ModId, "crafting_table_side", Resources.Load<Texture2D>("Textures/Blocks/crafting_table_side"));
        ResourceSystem.Instance.RegisterTexture(ModId, "crafting_table_top", Resources.Load<Texture2D>("Textures/Blocks/crafting_table_top"));
        ResourceSystem.Instance.RegisterTexture(ModId, "oak_log_side", Resources.Load<Texture2D>("Textures/Blocks/oak_log_side"));
        ResourceSystem.Instance.RegisterTexture(ModId, "oak_log_top_bottom", Resources.Load<Texture2D>("Textures/Blocks/oak_log_top_bottom"));
        ResourceSystem.Instance.RegisterTexture(ModId, "oak_sapling", Resources.Load<Texture2D>("Textures/Blocks/oak_sapling"));
        ResourceSystem.Instance.RegisterTexture(ModId, "oak_plank", Resources.Load<Texture2D>("Textures/Blocks/oak_plank"));
        // Leaves: one neutral greyscale base map (white_leaf) is shared by every
        // leaf species; the species colour is baked in at registration. The base
        // map itself is a working asset and is not registered.
        ResourceSystem.Instance.RegisterTexture(ModId, "oak_leaves",
            TintedTexture(Resources.Load<Texture2D>("Textures/Blocks/white_leaf"), OakLeavesTint));
    }

    // Species colour of oak leaves (design doc 树木系统 §3.5). Vanilla
    // semantics: the rendered leaf = base map * tint, multiplied per channel as
    // sRGB bytes (no linear-space conversion), alpha kept as-is.
    private static readonly Color32 OakLeavesTint = new(93, 167, 23, 255);

    // CPU-side tint of a greyscale base map, producing a fresh readable
    // texture for the atlas packer (the source asset is never modified).
    private static Texture2D TintedTexture(Texture2D source, Color32 tint)
    {
        var pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i].r = (byte)(pixels[i].r * tint.r / 255);
            pixels[i].g = (byte)(pixels[i].g * tint.g / 255);
            pixels[i].b = (byte)(pixels[i].b * tint.b / 255);
        }
        var tinted = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        tinted.SetPixels32(pixels);
        tinted.Apply();
        return tinted;
    }

    // ---- R: crack overlay textures ----
    private void RegisterCrackTextures()
    {
        //Break block sprite
        for(int i = 0; i < 10; i++)
        {
            ResourceSystem.Instance.RegisterTexture(ModId, $"crack_{i}",
                Resources.Load<Texture2D>($"Textures/Overlay/breakStageSprite-{i}")
            );
        }
    }
}
