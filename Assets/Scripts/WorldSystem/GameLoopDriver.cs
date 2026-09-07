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

    private float accumulator;

    private void Update()
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
        GameClock.Alpha = Mathf.Clamp01(accumulator / GameClock.TickInterval);
    }

    private void OnApplicationQuit()
    {
        WorldSaveManager.Instance.SaveAllOnQuit();
    }
}
