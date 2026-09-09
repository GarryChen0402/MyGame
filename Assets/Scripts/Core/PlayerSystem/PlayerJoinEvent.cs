// Logical "player joined the world" event (decision D, aligned with MC
// PlayerList.placeNewPlayer / ServerPlayer): published once per enter-world
// sequence, just before the logic gate opens. The world is ready and the
// player is settled but still frozen - from the next tick on, "the player
// entered the ticking world" is a fact. Empty payload: the joined state is
// queryable through Player.Instance / mirrors.
public class PlayerJoinEvent : GameEvent
{
}
