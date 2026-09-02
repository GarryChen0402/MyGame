using UnityEngine;

// Sole frame entry that steps the world logic: feeds Unity's deltaTime into
// WorldManager.Tick every frame (design doc 逻辑tick驱动与渲染解耦重构方案.md).
// GameBootstrap Phase 1 creates this as a persistent GO, so the same driver
// exists in every mode (editor play, builds, Unity Dedicated Server) and
// WorldRenderer stays a pure render host. Quit-time saving lives here too - a
// renderer-less server build must still flush the world when it exits.
public class GameLoopDriver : MonoBehaviour
{
    private void Update()
    {
        WorldManager.Instance.Tick(Time.deltaTime);
    }

    private void OnApplicationQuit()
    {
        WorldSaveManager.Instance.SaveAllOnQuit();
    }
}
