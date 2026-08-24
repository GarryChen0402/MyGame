using UnityEngine;

public static class NeighborDirs
{
    public static readonly Vector3Int[] NeightborDir = new Vector3Int[]
    {
        new Vector3Int(0, 0, -1),
        new Vector3Int(0, 0,  1),
        new Vector3Int(0, -1, 0),
        new Vector3Int(0,  1, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int( 1, 0, 0)
    };
}