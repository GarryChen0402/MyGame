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
