using UnityEngine;

public class Player : Entity
{
    private static Player instance = new();
    public static Player Instance => instance;

    // MC: 1.8-tall player box, eyes sit at 1.62 above the feet.
    public const float EyeHeight = 1.62f;


    private Player()
    {
        // Spawn above the tallest biome surface (mountains reach ~106) so the
        // player falls onto the world from above; the box pivot lands on (0, 115, 0).
        AABBs.Add(new AABB()
        {
            MinRange = new Vector3(-0.3f, 114.1f, -0.3f),
            MaxRange = new Vector3( 0.3f, 115.9f, 0.3f)
        });

        inventory = new Inventory(36, false);
    }
}