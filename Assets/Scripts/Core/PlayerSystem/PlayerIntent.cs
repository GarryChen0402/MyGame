using UnityEngine;

// Command boundary between the input frames and the game tick (rules M1/A1,
// design doc 玩家权威化与输入命令化-代码设计 §3): the input layer only writes
// Player.Intent, the player's tick consumes it. move is continuous state -
// overwritten every input frame, cleared after each consume - while
// jumpRequested and action are edge state: consumed and cleared exactly once,
// never replayed (rule B5).
public class PlayerIntent
{
    public Vector2 move;       // look-space input axes: x = forward(+)/back(-), y = right(+)/left(-); frame overwritten
    public bool jumpRequested; // jump edge (WasPressed); consume-and-clear
    public PlayerActionRequest action;   // action single slot; null = none; consume-and-clear
}

// Action intent: the click frame resolves the target (what the crosshair saw
// and what was held); the logic-side entry re-validates legality - target still
// present, within reach - before any session starts (rules A1/A2).
public enum PlayerActionKind { AttackBlock, AttackEntity, Use }

public class PlayerActionRequest
{
    public PlayerActionKind kind;
    public Vector3Int blockCoord;   // AttackBlock / Use: the click-frame block target
    public MobEntity entityTarget;  // AttackEntity: the click-frame mob target
    public bool isBlockHit;         // Use: the click hit a block (else item used on air)
    public ItemStack heldStack;     // Use: held stack resolved at the click frame
}
