using Unity.VisualScripting;
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

    private const float RaycastReach = 4.5f;
    private int CurrentSelectedSlotIndex = 0;
    private void Awake()
    {
        player = Player.Instance;

        player.inventory.TryAddItemAsMax(new ItemStack(){itemId = 0, amount = 1});
        player.inventory.TryAddItemAsMax(new ItemStack(){itemId = 1, amount = 1});
        player.inventory.TryAddItemAsMax(new ItemStack(){itemId = 2, amount = 1});
        player.inventory.TryAddItemAsMax(new ItemStack(){itemId = 3, amount = 16});
        player.inventory.TryAddItemAsMax(new ItemStack(){itemId = 4, amount = 16});
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

        
        MoveHandler();
        InteractionHandler();

    }
    private void MoveHandler()
    {
        player.yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        player.pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        player.pitch = Mathf.Clamp(player.pitch, -90f, 90f);

        // Raycast from the eyes toward the crosshair; store the result on the player.
        Vector3 eye = player.Position + Vector3.up * Player.EyeHeight;
        Vector3 dir = Quaternion.Euler(player.pitch, player.yaw, 0) * Vector3.forward;
        RaycastHit hit = default;
        if(WorldManager.Instance.TryGetDimension(player.DimensionId, out Dimension dim))
            hit = Raycaster.Raycast(dim, eye, dir, RaycastReach, out hit) ? hit : default;
        player.CurrentRaycastHitResult = hit;
        // if(hit.IsHit)
        //     Debug.Log($"Looking at block {hit.BlockDimensionCoord}, dist {hit.Distance:F2}, normal {hit.Normal}");
        // else
        //     Debug.Log("Looking at air");

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
        Vector3 right = new(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));   // 绕 Y 顺时针 90°
        Vector3 motion = (moveDirection * moveDir.x + right * moveDir.y) * horizontalMoveSpeed
                       + Vector3.up * horizontalMove * verticalMoveSpeed;
        motion *= Time.deltaTime;

        player.Move(motion);
    }

    private void InteractionHandler()
    {
        // Mouse wheel cycles the selected inventory slot (wraps around).
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if(scroll != 0f && player.inventory.itemStacks.Count > 0)
        {
            CurrentSelectedSlotIndex += scroll > 0f ? 1 : -1;
            int count = player.inventory.itemStacks.Count;
            CurrentSelectedSlotIndex = ((CurrentSelectedSlotIndex % count) + count) % count;

            // Log the item now selected in the new slot.
            ItemStack selected = player.inventory.GetItemStackAt(CurrentSelectedSlotIndex);
            if(selected != null && !selected.IsEmpty() &&
               ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(selected.itemId, out var selectedDef))
                Debug.Log($"Slot {CurrentSelectedSlotIndex}: {selectedDef.FullName} x{selected.amount}");
            else
                Debug.Log($"Slot {CurrentSelectedSlotIndex}: (empty)");
        }

        if(Input.GetMouseButtonDown(0) && player.CurrentRaycastHitResult.IsHit)
        {
            Debug.Log(WorldManager.Instance.TryBreakBlockAt(player.DimensionId, player.CurrentRaycastHitResult.BlockDimensionCoord, fromInteraction: true));
        }
        if(Input.GetMouseButtonDown(1) && player.CurrentRaycastHitResult.IsHit)
        {
            ItemStack stack = player.inventory.GetItemStackAt(CurrentSelectedSlotIndex);
            if(stack == null || stack.IsEmpty())return;
            if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(stack.itemId, out var itemDef))return;
            if(!ResourceSystem.Instance.ItemBehaviors.TryGetResourceWithFullName(itemDef.ItemBehaivorId, out var behavior))return;
            var res = behavior.OnRightUseToBlock(player, stack);
            if(res.UseSuccess)player.inventory.TryConsumeItemAt(CurrentSelectedSlotIndex, res.ConsumeAmount);
            Debug.Log($"{res.UseSuccess} == {res.ConsumeAmount}");
        }
    }
}   