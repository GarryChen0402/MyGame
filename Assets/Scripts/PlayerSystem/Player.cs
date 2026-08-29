using UnityEngine;

public class Player : Entity
{
    private static Player instance = new();
    public static Player Instance => instance;

    // MC: 1.8-tall player box, eyes sit at 1.62 above the feet.
    public const float EyeHeight = 1.62f;


    private Player()
    {
        // Spawn 40 blocks up so the player falls onto the world from above;
        // the box pivot (Position) lands exactly on (0, 40, 0).
        AABBs.Add(new AABB()
        {
            MinRange = new Vector3(-0.3f, 39.1f, -0.3f),
            MaxRange = new Vector3( 0.3f, 40.9f, 0.3f)
        });
    }
}