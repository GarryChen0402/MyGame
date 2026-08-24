
public class TestWorldGenerator : IWorldGenerator
{
    public void GenerateChunk(Chunk chunk)
    {
        if(!ResourceSystem.Instance.BlockDefinitions.TryGetNumberId("minecraft:stone", out var def))return;

        for(int y = CoordUtils.MinSubChunkYIndex * CoordUtils.BlockSizePerChunk; y < CoordUtils.MaxSubChunkYIndex * CoordUtils.BlockSizePerChunk; y++)
        {
            for(int x = 0; x < CoordUtils.BlockSizePerChunk; x++)
            {
                for(int z = 0; z < CoordUtils.BlockSizePerChunk; z++)
                {
                    chunk.TrySetBlockAt(x, y, z, def);
                }
            }    
        }
    }
}