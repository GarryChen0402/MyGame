using UnityEngine;

// Sole frame entry that steps the world logic on a fixed 20Hz game clock
// (design doc 固定Tick时钟与渲染插值改造-代码设计.md): each render frame
// feeds Time.deltaTime into an accumulator, runs as many fixed ticks as are
// due (capped per frame - a hitch never spirals; the backlog is kept, not
// dropped), then publishes the leftover as GameClock.Alpha so render shells
// can interpolate between tick states (partial tick, MC Timer alike).
// GameBootstrap Phase 1 creates this as a persistent GO, so the same driver
// exists in every mode (editor play, builds, Unity Dedicated Server) and
// WorldRenderer stays a pure render host. Quit-time saving lives here too - a
// renderer-less server build must still flush the world when it exits.
[DefaultExecutionOrder(-50)]   // after the input layer (-100), before render shells (0)
public class GameLoopDriver : MonoBehaviour
{
    private const int MaxCatchUpTicks = 5;

    // Debug acceptance toggle (rule R-C2-7, delivered at the end of C-1):
    // freezes tick settlement while render frames keep running. Mirror sync
    // still runs under the freeze but copies unchanged values - so the UI
    // going still is a structural guarantee, not a render bug. The
    // accumulator is not drained, so unfreezing never bursts the backlog.
    public static bool PauseLogic;

    private float accumulator;

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.F8))
        {
            PauseLogic = !PauseLogic;
            Debug.Log($"[GameLoopDriver] logic {(PauseLogic ? "FROZEN (F8 to resume)" : "resumed")}");
        }
        if(!PauseLogic)
        {
            accumulator += Time.deltaTime;
            int ticks = 0;
            while(accumulator >= GameClock.TickInterval && ticks < MaxCatchUpTicks)
            {
                accumulator -= GameClock.TickInterval;
                GameClock.TickCount++;
                WorldManager.Instance.Tick(GameClock.TickInterval);
                ticks++;
            }
        }
        else if(WorldManager.Instance.Dimensions.Count > 0)
        {
            // Frozen-frame pump (Part B §4.4): async chunk readiness and the
            // loading progress advance only through this exception while the
            // logic is frozen (enter-world / loading). No double pump when
            // unfrozen - Tick owns the pump in the game state. Dimensions
            // empty (main menu) means zero work and no queue to pump.
            WorldManager.Instance.PumpChunkGeneration();
        }
        GameClock.Alpha = PauseLogic ? 0f : Mathf.Clamp01(accumulator / GameClock.TickInterval);

        // Mirror sync point (Phase C rule R-C1-0): after the fixed ticks ran
        // (or after a frozen frame, copying identical values), refresh every
        // bound mirror for the render side of this frame.
        MirrorSync.Instance.Sync();
    }

    private void OnApplicationQuit()
    {
        WorldSaveManager.Instance.SaveAllOnQuit();
    }
}
