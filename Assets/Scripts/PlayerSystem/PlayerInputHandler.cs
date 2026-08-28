using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField]
    private Player player = null;

    [SerializeField]
    private float horizontalMoveSpeed;

    [SerializeField]
    private float verticalMoveSpeed;

    [SerializeField]
    private float mouseSensitivity = 2f;

    private void Awake()
    {
        player = Player.Instance;
    }

    private void Update()
    {
        if(player == null)return;
        if (Input.GetKey(KeyCode.LeftAlt))
        {
            Cursor.lockState = CursorLockMode.None;
            return;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        player.yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        player.pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        player.pitch = Mathf.Clamp(player.pitch, -90f, 90f);

        Vector2 moveDir = Vector2.zero;
        int horizontalMove = 0;
        if(Input.GetKey(KeyCode.W))moveDir.x += 1;
        if(Input.GetKey(KeyCode.S))moveDir.x -= 1;
        if(Input.GetKey(KeyCode.A))moveDir.y -= 1;
        if(Input.GetKey(KeyCode.D))moveDir.y += 1;
        if(Input.GetKey(KeyCode.Space))horizontalMove += 1;
        if(Input.GetKey(KeyCode.LeftShift))horizontalMove -= 1;

        float yawRad = player.yaw * Mathf.Deg2Rad;
        Vector3 moveDirection = new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad)).normalized;
        Vector3 right = new Vector3(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));   // 绕 Y 顺时针 90°
        Vector3 motion = (moveDirection * moveDir.x + right * moveDir.y) * horizontalMoveSpeed
                       + Vector3.up * horizontalMove * verticalMoveSpeed;
        motion *= Time.deltaTime;

        player.Move(motion);

    }
}   