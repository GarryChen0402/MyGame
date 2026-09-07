using UnityEngine;

// Pure-data mirror of the player render-read state (rule R-C2-5): camera
// follow group + the input-layer ray frame data (rule R-C1-5 render read side;
// only the pure coordinate fields cross - HitEntity etc. stay logic-side).
// Written only by the MirrorSync sync point (PlayerBinding), read by
// PlayerRenderer and the WorldRenderer sort center. No visible body shell.
public class PlayerMirror
{
    public Vector3 PrevPosition;
    public Vector3 Position;
    public float Pitch, Yaw;      // input-written every render frame; no interpolation
    public float EyeHeight;
    public ushort DimensionId;
    public bool RayIsHit;         // crosshair raycast frame data (gizmo reads)
    public Vector3Int RayBlockCoord;

    public void ApplyFrom(Player src)
    {
        if(src == null)return;
        PrevPosition = src.PrevPosition;
        Position = src.Position;
        Pitch = src.pitch;
        Yaw = src.yaw;
        EyeHeight = Player.EyeHeight;
        DimensionId = src.DimensionId;
        var hit = src.CurrentRaycastHitResult;
        RayIsHit = hit.IsHit;
        RayBlockCoord = hit.BlockDimensionCoord;
    }
}
