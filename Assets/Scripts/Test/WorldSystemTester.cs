using System.Collections;
using UnityEngine;

// Demo script: registers resources, generates the test dimension, loads a diamond
// of chunks around the origin and lets WorldRenderer display them. No assertions -
// run in Play mode and inspect the rendered chunks visually.
public class WorldSystemTester : MonoBehaviour
{
    private void Start() => StartCoroutine(Run());

    private IEnumerator Run()
    {
        // Resource registration and atlas packing are handled by GameBootstrap.
        // Restore world seed and player state before the world builds around them.
        // TODO(Phase 6): main menu / world selection UI is not implemented yet -
        // this entry point should let the player choose "load an existing save" or
        // "start a new world" (see Docs/游戏启动与资源初始化阶段设计.md, Phase 6-8).
        // For now the game always enters the fixed save directory directly.
        WorldSaveManager.Instance.LoadWorldMeta();
        WorldSaveManager.Instance.LoadPlayer();

        string mod = Minecraft.ModId;
        string dimName = $"{mod}:test_dim";
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(dimName, out ushort dimId)) yield break;
        if(!WorldManager.Instance.TryGetOrGenerateDimension(dimName, out Dimension dim)) yield break;

        // Load a diamond of chunks (Manhattan distance <= range) around the origin
        var center = new Vector2Int(0, 0);
        int range = 2;
        for(int x = -range; x <= range; x++)
        {
            int maxZ = range - Mathf.Abs(x);
            for(int z = -maxZ; z <= maxZ; z++)
                dim.LoadChunk(center + new Vector2Int(x, z));
        }

        var wr = Object.FindFirstObjectByType<WorldRenderer>(FindObjectsInactive.Exclude);
        if(wr == null) yield break;
        // A restored player position must become the chunk-loading center.
        wr.SetPlayerPosition(Player.Instance.Position);
        wr.SetRenderDimension(dimId);

        // Move the camera above the world for a clear overview
        var camera = Camera.main;
        if(camera != null)
        {
            camera.transform.position = new Vector3(0.5f, 80f, 0.5f);
            camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        // World is built; join the player so the game logic can start.
        _ = Player.Instance;
    }
}
