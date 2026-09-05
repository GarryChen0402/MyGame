// Player dead event: published by the Player numeric layer (PlayerSystem,
// step-2) when health hits zero; MobAI subscribes to release its chase target
// lock (design doc §2.1). Event class only - the publish point lands with the
// player health layer.
public class PlayerDeadEvent : GameEvent
{
}
