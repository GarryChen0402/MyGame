using UnityEngine;

public class PlayerRenderer : MonoBehaviour
{
    [SerializeField]
    private Player player = null;

    [SerializeField]
    private Camera playerCamera = null;

    private void Awake()
    {
        if(player == null) player = Player.Instance;
        if(playerCamera == null) playerCamera = Camera.main;
    }

    // LateUpdate so the camera follows after physics/input move the player.
    private void LateUpdate()
    {
        playerCamera.transform.position = player.Position + Vector3.up * Player.EyeHeight;
        playerCamera.transform.rotation = Quaternion.Euler(player.pitch, player.yaw, 0);
    }

    // Yellow wireframe over the collision boxes of the currently targeted block.
    private void OnDrawGizmos()
    {
        // Gizmos also run in edit mode where Awake never fired, so the player
        // reference may be null here.
        if(player == null) return;
        RaycastHit hit = player.CurrentRaycastHitResult;
        if(!hit.IsHit) return;
        if(!WorldManager.Instance.TryGetDimension(player.DimensionId, out Dimension dim)) return;

        Gizmos.color = Color.yellow;
        PhysicsManager.ForEachBlockCollisionBox(dim, hit.BlockDimensionCoord, box =>
        {
            Vector3 center = (box.MinRange + box.MaxRange) * 0.5f;
            Gizmos.DrawWireCube(center, box.MaxRange - box.MinRange);
        });
    }
}
