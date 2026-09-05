using System.Collections.Generic;
using UnityEngine;

// A* block-grid follower shared by Chase and the Attack state's approach move
// (design doc §4). Owns the path + waypoint cursor; recomputes on the sense
// cadence so a moving player never chases a stale route for long. Produces
// Intent.MoveDirection and - for one-cell step-up edges - Intent.JumpRequested;
// the execution-layer contract of the skeleton is otherwise unchanged.
public class ZombieAIPath
{
    private readonly List<Vector3> waypoints = new();   // cell run as world centers; y carries the stand-cell height for step-up
    private int cursor;                                  // index of the waypoint currently aimed at
    private float lastRecomputeTime = float.MinValue;    // timestamp, not accumulator: stale after any wander gap
    private bool failed;                                 // last search failed: straight-line fallback until the next retry

    public const float RecomputeInterval = 0.1f;         // sense cadence sync (design doc §2.1)
    private const float WaypointReachDistance = 0.45f;   // horizontal reach: cell pitch is 1, so no corner is skipped
    private const int MaxExpandedNodes = 2048;           // per-search budget (the linear open scan stays cheap inside it)

    public void Follow(AIContext ctx, AIIntent intent, float dt)
    {
        Entity target = ctx.Target;
        if(target == null)
        {
            intent.MoveDirection = Vector3.zero;
            return;
        }
        if(Time.time - lastRecomputeTime >= RecomputeInterval)
            Recompute(ctx.Self, target);

        MobEntity mob = ctx.Self;
        Vector3 pos = mob.Position;
        float footY = mob.MainBox.MinRange.y;   // feet level; a waypoint's y is its cell's center

        // Consume waypoints the mob is horizontally near AND level with. A
        // step-up waypoint one cell higher is not consumed until the mob
        // actually stands on it (vanilla: a path node counts as reached only
        // at its own height).
        while(cursor < waypoints.Count
              && HorizontalDistance(pos, waypoints[cursor]) < WaypointReachDistance
              && Mathf.Abs(waypoints[cursor].y - (footY + 0.5f)) < 0.7f)
            cursor++;

        // Aim at the current waypoint, or straight at the target when none is
        // left (path exhausted, search failed, or not yet computed) - the
        // recompute tick restores a real path when one exists. Off-path drift
        // (knockback etc.) is not actively corrected (design doc §4.2): the
        // next recompute roots at the mob's current position anyway.
        Vector3 aim = cursor < waypoints.Count ? waypoints[cursor] : target.Position;
        bool stepUp = aim.y - footY > 0.9f;   // aim's stand cell one cell above the feet

        // Step-up request: aimed one cell higher and horizontal motion was just
        // cut by a wall (previous physics tick). Vanilla fires the jump from
        // the move control once the path demands a higher node - same shape.
        if(stepUp && mob.HorizontalBlocked)
            intent.JumpRequested = true;

        Vector3 d = aim - pos;
        d.y = 0f;
        if(d.sqrMagnitude < 1e-6f)
        {
            intent.MoveDirection = Vector3.zero;   // horizontally overlapped: no step to take
            return;
        }
        intent.MoveDirection = d.normalized;
    }

    private void Recompute(MobEntity mob, Entity target)
    {
        lastRecomputeTime = Time.time;
        failed = true;
        waypoints.Clear();
        cursor = 0;

        if(!WorldManager.Instance.TryGetDimension(mob.DimensionId, out Dimension dim))return;
        Vector3Int startCell = Dimension.WorldPosToDimensionCoord(mob.Position);
        Vector3Int goalCell = Dimension.WorldPosToDimensionCoord(target.Position);
        if(!TryFindPath(dim, startCell, goalCell, waypoints))return;
        failed = false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private readonly struct AStarNode
    {
        public readonly Vector3Int Cell;
        public readonly float G;
        public readonly float F;
        public readonly Vector3Int Parent;   // the cell this one was reached from (start's parent is itself)

        public AStarNode(Vector3Int cell, float g, float f, Vector3Int parent)
        {
            Cell = cell;
            G = g;
            F = f;
            Parent = parent;
        }
    }

    private static readonly Vector3Int[] NeighborOffsets =
    {
        new( 1, 0, 0), new(-1, 0, 0), new(0, 0, 1), new(0, 0, -1)
    };

    // Returns false on failure (no route / budget exceeded). Search is
    // 4-neighbor on x/z with one-cell step-up edges (vanilla WalkNodeEvaluator
    // upward lookup): cells are stand cells, so a path can climb 1-block
    // ledges but never 2-block walls. A goal on a lower or unreachable level
    // simply exhausts the search and degrades to the straight-line fallback.
    private static bool TryFindPath(Dimension dim, Vector3Int startCell, Vector3Int goalCell, List<Vector3> outWaypoints)
    {
        outWaypoints.Clear();
        AIPathDebug.Begin(startCell, goalCell);

        var open = new Dictionary<Vector3Int, AStarNode>();
        var closed = new Dictionary<Vector3Int, AStarNode>();
        var cellPath = new List<Vector3Int>();
        int expanded = 0;
        open[startCell] = new AStarNode(startCell, 0f, Heuristic(startCell, goalCell), startCell);

        while(open.Count > 0)
        {
            Vector3Int best = MinF(open);   // naive linear scan: fine inside the node budget
            AStarNode node = open[best];
            open.Remove(best);

            if(node.Cell == goalCell)
            {
                // Chain back goal -> ... -> start; the start cell itself is
                // dropped (the mob stands in it - aiming at its center would
                // backtrack). Every parent is already expanded, so closed
                // lookups always hit.
                AStarNode n = node;
                Vector3Int cur = n.Cell;
                while(cur != startCell)
                {
                    cellPath.Add(cur);
                    cur = n.Parent;
                    n = closed[cur];
                }
                cellPath.Reverse();
                foreach(Vector3Int c in cellPath)
                    outWaypoints.Add(new Vector3(c.x + 0.5f, c.y + 0.5f, c.z + 0.5f));   // block-center world positions
                AIPathDebug.End(true, cellPath);
                return true;
            }

            closed[node.Cell] = node;
            if(AIPathDebug.Enabled)AIPathDebug.Closed.Add(node.Cell);
            if(++expanded > MaxExpandedNodes)
            {
                AIPathDebug.End(false);
                return false;
            }
            foreach(Vector3Int off in NeighborOffsets)
            {
                Vector3Int nb = node.Cell + off;
                if(closed.ContainsKey(nb))continue;
                if(IsWalkable(dim, nb))
                {
                    Offer(nb, node, goalCell, open);
                    continue;
                }
                // Step-up: the same-level neighbor is a wall/ledge - offer the
                // cell one higher when walkable (headroom and support are part
                // of IsWalkable, so a 2-block wall and overhangs drop out).
                Vector3Int nbUp = nb + Vector3Int.up;
                if(!closed.ContainsKey(nbUp))Offer(nbUp, node, goalCell, open);
            }
        }
        AIPathDebug.End(false);
        return false;
    }

    // Open-set upsert shared by the flat and the step-up neighbor offers.
    private static void Offer(Vector3Int cell, AStarNode from, Vector3Int goalCell, Dictionary<Vector3Int, AStarNode> open)
    {
        float g = from.G + 1f;
        if(open.TryGetValue(cell, out AStarNode known) && known.G <= g)return;
        if(AIPathDebug.Enabled)AIPathDebug.Open.Add(cell);
        open[cell] = new AStarNode(cell, g, g + Heuristic(cell, goalCell), from.Cell);
    }

    // Walkable = air at the stand cell + solid below (support) + air above
    // (headroom for the 1.8-tall box, same scale as collision). Unloaded
    // chunks report air too, so their below reads 0: the search never steps
    // off into unloaded territory on its own (design doc §4.3).
    private static bool IsWalkable(Dimension dim, Vector3Int cell)
    {
        if(dim.GetBlockAt(cell) != 0)return false;
        if(dim.GetBlockAt(cell + Vector3Int.down) == 0)return false;
        return dim.GetBlockAt(cell + Vector3Int.up) == 0;
    }

    // Euclidean horizontal distance to the goal (admissible for the
    // uniform-cost 4-neighbor grid).
    private static float Heuristic(Vector3Int a, Vector3Int b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static Vector3Int MinF(Dictionary<Vector3Int, AStarNode> open)
    {
        Vector3Int best = default;
        float bestF = float.PositiveInfinity;
        foreach(var pair in open)
        {
            if(pair.Value.F < bestF)
            {
                bestF = pair.Value.F;
                best = pair.Key;
            }
        }
        return best;
    }
}

// Static snapshot of the last path query, drawn by AIPathDebugDrawer in the
// Scene view (design doc §4.4). v1 is single-mob debug: later queries
// overwrite earlier ones - per-mob coloring waits until needed. Enabled is a
// manual toggle; every method early-outs off it.
public static class AIPathDebug
{
    // Default-on while chasing/debugging v1; flip to false to silence the
    // Scene-view overlay (drawing only happens on real path queries).
    public static bool Enabled = true;
    public static bool HasQuery;
    public static Vector3Int Start;
    public static Vector3Int Goal;
    public static HashSet<Vector3Int> Open = new();       // swapped by reference per query (no clear churn)
    public static HashSet<Vector3Int> Closed = new();
    public static List<Vector3Int> PathCells = new();

    public static void Begin(Vector3Int start, Vector3Int goal)
    {
        if(!Enabled)return;
        HasQuery = false;
        Start = start;
        Goal = goal;
        Open = new HashSet<Vector3Int>();
        Closed = new HashSet<Vector3Int>();
        PathCells = new List<Vector3Int>();
    }

    public static void End(bool success, List<Vector3Int> pathCells = null)
    {
        if(!Enabled)return;
        HasQuery = true;
        if(success && pathCells != null)PathCells.AddRange(pathCells);
        AIPathDebugDrawer.Ensure();
    }
}
