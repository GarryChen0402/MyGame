using UnityEngine;

// Scene-view host for AIPathDebug's static snapshot (design doc §4.4): wire
// cubes for the open/closed exploration and a polyline for the final path.
// Lazily created on the first recorded query and parented under WorldRenderer
// like the other runtime roots; draws nothing while AIPathDebug.Enabled is off.
public class AIPathDebugDrawer : MonoBehaviour
{
    private static AIPathDebugDrawer instance;

    public static void Ensure()
    {
        if(instance != null)return;
        var go = new GameObject("AIPathDebug");
        if(WorldRenderer.Instance != null)
            go.transform.SetParent(WorldRenderer.Instance.transform, false);
        instance = go.AddComponent<AIPathDebugDrawer>();
    }

    private void OnDrawGizmos()
    {
        if(!AIPathDebug.Enabled || !AIPathDebug.HasQuery)return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        foreach(var c in AIPathDebug.Closed)DrawCell(c);
        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.35f);
        foreach(var c in AIPathDebug.Open)DrawCell(c);

        Gizmos.color = Color.green;
        DrawCell(AIPathDebug.Start);
        Gizmos.color = Color.magenta;
        DrawCell(AIPathDebug.Goal);

        if(AIPathDebug.PathCells.Count > 0)
        {
            Gizmos.color = Color.yellow;
            var cells = AIPathDebug.PathCells;
            Vector3 prev = CellCenter(cells[0]);
            for(int i = 1; i < cells.Count; i++)
            {
                Vector3 next = CellCenter(cells[i]);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }

    private static Vector3 CellCenter(Vector3Int c) => new(c.x + 0.5f, c.y + 0.5f, c.z + 0.5f);
    private static void DrawCell(Vector3Int c) => Gizmos.DrawWireCube(CellCenter(c), Vector3.one);
}
