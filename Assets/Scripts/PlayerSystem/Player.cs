using UnityEngine;

public class Player : Entity
{
    private static Player instance = new();
    public static Player Instance => instance;

    private Player()
    {
        AABBs.Add(new AABB()
        {
            MinRange = new Vector3(-0.3f, 0, -0.3f),
            MaxRange = new Vector3( 0.3f, 1.8f, 0.3f)
        });
    }
}