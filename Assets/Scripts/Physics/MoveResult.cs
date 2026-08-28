using UnityEngine;

public readonly struct MoveResult
{
    public readonly Vector3 Motion;
    public readonly bool OnGround;
    public readonly bool HitCeiling;
    public readonly bool HitWall;
    public readonly bool Collided;

    public MoveResult(Vector3 motion, bool onGround, bool hitCeiling, bool hitWall, bool collided)
    {
        Motion = motion;
        OnGround = onGround;
        HitCeiling = hitCeiling;
        HitWall = hitWall;
        Collided = collided;
    }
}