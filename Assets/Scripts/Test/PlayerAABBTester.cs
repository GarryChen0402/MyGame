using UnityEngine;

// Test: spawns a Player, hooks up the PlayerRenderer view, and draws the
// entity's AABB wireframe every frame in the scene view.
public class PlayerAABBTester : MonoBehaviour
{
    private Player player;

    private void Start()
    {
        player = new Player(); // constructor publishes SummonEntity -> auto-registered
        // Spawn in the air: spawning inside a solid block would pin the entity
        // in place (every axis clamps to 0 contact).
        player.SetPosition(new Vector3(0f, 100f, 0f));

        var viewGo = new GameObject("PlayerView");
        var view = viewGo.AddComponent<PlayerRenderer>();
        view.Player = player;
    }

    private void Update()
    {
        if (player == null) return;
        MoveResult r = player.FlyMove(Time.deltaTime);
        if (r.Collided && Time.frameCount % 30 == 0)
            Debug.Log($"[PlayerAABBTester] blocked, applied motion={r.Motion}");
        DrawAABB(player.MainBox, Color.yellow);
    }

    private static void DrawAABB(AABB box, Color color)
    {
        Vector3 min = new(box.minX, box.minY, box.minZ);
        Vector3 max = new(box.maxX, box.maxY, box.maxZ);
        Vector3 p000 = min;
        Vector3 p100 = new(max.x, min.y, min.z);
        Vector3 p010 = new(min.x, max.y, min.z);
        Vector3 p001 = new(min.x, min.y, max.z);
        Vector3 p110 = new(max.x, max.y, min.z);
        Vector3 p101 = new(max.x, min.y, max.z);
        Vector3 p011 = new(min.x, max.y, max.z);
        Vector3 p111 = max;

        Debug.DrawLine(p000, p100, color);
        Debug.DrawLine(p000, p010, color);
        Debug.DrawLine(p000, p001, color);
        Debug.DrawLine(p111, p101, color);
        Debug.DrawLine(p111, p011, color);
        Debug.DrawLine(p111, p110, color);
        Debug.DrawLine(p100, p110, color);
        Debug.DrawLine(p100, p101, color);
        Debug.DrawLine(p010, p110, color);
        Debug.DrawLine(p010, p011, color);
        Debug.DrawLine(p001, p101, color);
        Debug.DrawLine(p001, p011, color);
    }
}
