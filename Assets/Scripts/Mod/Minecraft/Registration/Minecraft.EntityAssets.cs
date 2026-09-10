using UnityEngine;

public partial class Minecraft
{
    // ---- C: entity model + animations ----
    private void RegisterEntityAssets()
    {
        // EntityModel & EntityAnimation Content. Models register as source
        // descriptors {type, path}; cube data parses lazily on first render.
        ResourceSystem.Instance.EntityModels.Register(new EntityModel
        {
            modId = ModId,
            name = "player",
            SourceType = EntityModelSourceType.Json,
            SourcePath = "Models/entity/player"
        });
        ResourceSystem.Instance.EntityAnimations.Register(EntityAnimationParser.Parse(Resources.Load<TextAsset>("Animations/player_walk").text));
        ResourceSystem.Instance.EntityAnimations.Register(EntityAnimationParser.Parse(Resources.Load<TextAsset>("Animations/player_attack").text));
    }
}
