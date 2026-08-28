using UnityEngine;

public class PlayerRenderer : MonoBehaviour
{
    [SerializeField]
    private Player player = null;

    [SerializeField]
    private Camera playerCamera = null;

    // MC: 1.8-tall player box, eyes sit at 1.62 above the feet.
    private const float EyeHeight = 1.62f;

    private void Awake()
    {
        if(player == null) player = Player.Instance;
        if(playerCamera == null) playerCamera = Camera.main;
    }

    // LateUpdate so the camera follows after physics/input move the player.
    private void LateUpdate()
    {
        playerCamera.transform.position = player.Position + Vector3.up * EyeHeight;
        playerCamera.transform.rotation = Quaternion.Euler(player.pitch, player.yaw, 0);
    }
}
