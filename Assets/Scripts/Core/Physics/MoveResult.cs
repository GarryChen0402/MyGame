using UnityEngine;

public readonly struct MoveResult
{
    public readonly Vector3 Motion;
    public readonly bool OnGround;
    public readonly bool HitCelling;
    public readonly bool HitWall;
    public readonly bool Collided;

    public MoveResult(Vector3 motion, bool onground, bool hitcelling, bool hitwall, bool collied)
    {
        Motion = motion;
        OnGround = onground;
        HitCelling = hitcelling;
        HitWall = hitwall;
        Collided = collied;
    }
}