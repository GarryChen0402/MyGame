// Fixed game clock (rule doc 固定Tick时钟与渲染插值改造-规则设计.md): the
// logical step size is fixed at 1/20s everywhere - GameLoopDriver feeds
// TickInterval into WorldManager.Tick as dt. Alpha is the partial-tick ratio
// for the current render frame (MC getFrameTime alike): render shells lerp an
// entity's PrevPosition/Position with it. TickCount counts executed game ticks
// - a deterministic logical clock; Time.time stays banned in logic code.
public static class GameClock
{
    public const float TickInterval = 1f / 20f;
    public static int TickCount;
    public static float Alpha;
}
