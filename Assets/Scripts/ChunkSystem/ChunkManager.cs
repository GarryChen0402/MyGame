using System.Collections.Generic;
using UnityEngine;

public class ChunkManager : SingletonTemplate<ChunkManager>
{
    private World currentWorld = null;

    public Transform Target;
    [SerializeField] private int playerViewRange = 1;
    private Vector2Int playerLastChunkCoord = new Vector2Int(int.MaxValue, int.MaxValue);

    protected override void Awake()
    {
        base.Awake();
        new Minecraft().RegisterAllResources();
        ResourceSystem.Instance.BuildAtlas();


        currentWorld = new World();
        currentWorld.SetWorldGenerator(new TestWorldGenerator());
        playerLastChunkCoord = new Vector2Int(int.MaxValue, int.MaxValue);
    }

    private void Update()
    {
        if (Target == null) return;
        if (CoordUtils.WorldPosToChunkCoord(Target.position) != playerLastChunkCoord) RefreshWorld();
    }

    private void RefreshWorld()
    {
        // Collect the old view range into the unload set
        HashSet<Vector2Int> unload = new();
        for (int i = -playerViewRange; i <= playerViewRange; i++)
        for (int j = -playerViewRange; j <= playerViewRange; j++)
            unload.Add(new Vector2Int(i, j) + playerLastChunkCoord);

        playerLastChunkCoord = CoordUtils.WorldPosToChunkCoord(Target.position);

        // Load the new view range; coords still in the set are out of range
        for (int i = -playerViewRange; i <= playerViewRange; i++)
        for (int j = -playerViewRange; j <= playerViewRange; j++)
        {
            Vector2Int cur = new Vector2Int(i, j) + playerLastChunkCoord;
            currentWorld.LoadChunk(cur);
            unload.Remove(cur);
        }

        foreach (var coord in unload) currentWorld.UnloadChunk(coord);
    }
}
