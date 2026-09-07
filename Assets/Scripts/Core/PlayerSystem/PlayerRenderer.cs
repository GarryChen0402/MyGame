using UnityEngine;

// Camera shell for the player (rule R-C2-5): reads the PlayerMirror one-way
// every frame - never touches the Player entity. Position lerps between tick
// states (player logic steps at 20Hz); the look rotation reads the mirror's
// yaw/pitch directly - MirrorSync refreshes those every render frame, so they
// need no interpolation. Only the gizmo debug path keeps geometry queries
// (PhysicsManager block boxes, D3-exempt).
public class PlayerRenderer : MonoBehaviour
{
    [SerializeField]
    private Camera playerCamera = null;

    private void Awake()
    {
        if(playerCamera == null) playerCamera = Camera.main;
    }

    private void LateUpdate()
    {
        var mirror = MirrorSync.Instance != null ? MirrorSync.Instance.PlayerMirror : null;
        if(playerCamera == null || mirror == null) return;
        playerCamera.transform.position = Vector3.Lerp(mirror.PrevPosition, mirror.Position, GameClock.Alpha)
            + Vector3.up * mirror.EyeHeight;
        playerCamera.transform.rotation = Quaternion.Euler(mirror.Pitch, mirror.Yaw, 0);
    }

    // Yellow wireframe over the collision boxes of the currently targeted block.
    private void OnDrawGizmos()
    {
        // Gizmos also run in edit mode where Awake never fired, so the mirror
        // may be null here.
        var mirror = MirrorSync.Instance != null ? MirrorSync.Instance.PlayerMirror : null;
        if(mirror == null || !mirror.RayIsHit) return;
        if(!WorldManager.Instance.TryGetDimension(mirror.DimensionId, out Dimension dim)) return;

        Gizmos.color = Color.yellow;
        PhysicsManager.ForEachBlockCollisionBox(dim, mirror.RayBlockCoord, box =>
        {
            Vector3 center = (box.MinRange + box.MaxRange) * 0.5f;
            Gizmos.DrawWireCube(center, box.MaxRange - box.MinRange);
        });
    }
}
