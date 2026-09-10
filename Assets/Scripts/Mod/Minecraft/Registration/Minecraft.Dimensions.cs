public partial class Minecraft
{
    // ---- F: dimension generators + definition ----
    private void RegisterDimensions()
    {
        DimensionGeneratorResource testGenerator = new()
        {
            modId = ModId,
            name = "test_dim_generator",
            GetNewGenerator = ()=> new TestDimensionGenerator()
        };
        ResourceSystem.Instance.DimensionGenerator.Register(testGenerator);
        DimensionGeneratorResource biomeGenerator = new()
        {
            modId = ModId,
            name = "biome_dim_generator",
            GetNewGenerator = () => new BiomeDimensionGenerator()
        };
        ResourceSystem.Instance.DimensionGenerator.Register(biomeGenerator);
        ResourceSystem.Instance.DimensionDefinitions.Register(testDi);
    }
}
