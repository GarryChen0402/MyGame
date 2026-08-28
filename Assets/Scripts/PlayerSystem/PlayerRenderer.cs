using UnityEngine;

// View layer for a pure-C# Player entity: keeps the player model visual glued
// to the entity's AABB pivot (bottom-center) every frame.
public class PlayerRenderer : MonoBehaviour
{
    private const string ModelPath = "Models/Entities/player";
    private const float EyeHeightRatio = 0.9f; // eyes sit near the top of the AABB

    public float MouseSensitivity = 2f;
    public float MaxPitch = 90f;

    public Player Player;

    private Transform visual;
    private Camera mainCamera;
    private float yaw;
    private float pitch;

    private void Start()
    {
        if (Player == null) return;
        visual = CreateVisual();
        mainCamera = Camera.main;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Player == null) return;
        Vector3 pivot = Player.Position;
        transform.position = pivot;

        // Mouse look: yaw spins around Y, pitch tilts up/down, clamped to avoid
        // flipping past the vertical.
        yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * MouseSensitivity, -MaxPitch, MaxPitch);

        if (mainCamera != null)
        {
            AABB box = Player.MainBox;
            float eyeHeight = (box.maxY - box.minY) * EyeHeightRatio;
            mainCamera.transform.position = pivot + new Vector3(0f, eyeHeight, 0f);
            mainCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private Transform CreateVisual()
    {
        GameObject model = Resources.Load<GameObject>(ModelPath);
        if (model != null)
            return Instantiate(model, transform).transform;

        // Fallback: a cube matching the AABB size when the model is missing.
        AABB box = Player.MainBox;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(transform, false);
        // Cube pivot is centered; our AABB pivot is at the bottom, so offset up
        // by half the box height.
        cube.transform.localPosition = new Vector3(0f, (box.maxY - box.minY) * 0.5f, 0f);
        cube.transform.localScale = new Vector3(box.maxX - box.minX, box.maxY - box.minY, box.maxZ - box.minZ);
        return cube.transform;
    }
}
