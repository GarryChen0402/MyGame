using UnityEngine;

// Runs before WorldRenderer.Update so a block edit marks its chunk rebuild in
// the same frame, letting the Important rebuild dispatch and render immediately.
// [DefaultExecutionOrder(-100)]
public class PlayerInputHandler : IInputHandler
{
    private Player player = null;

    private float horizontalMoveSpeed = 5;

    private float verticalMoveSpeed = 5;

    private float mouseSensitivity = 2f;

    private const float RaycastReach = 4.5f;
    public PlayerInputHandler()
    {
        player = Player.Instance;
        modId = "minecraft";
        name = "player_input_handler";
    }

    public override void OnUpdate()
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
            player.SelectedSlotIndex += scroll > 0f ? 1 : -1;
            // int count = player.inventory.itemStacks.Count;
            int count = 9;
            player.SelectedSlotIndex = ((player.SelectedSlotIndex % count) + count) % count;

            // Log the item now selected in the new slot.
            ItemStack selected = player.inventory.GetItemStackAt(player.SelectedSlotIndex);
            if(selected != null && !selected.IsEmpty() &&
               ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(selected.itemId, out var selectedDef))
                Debug.Log($"Slot {player.SelectedSlotIndex}: {selectedDef.FullName} x{selected.amount}");
            else
                Debug.Log($"Slot {player.SelectedSlotIndex}: (empty)");
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (player.IsHoldingItem())
            {
                ItemUseResult result = new();
                ItemStack currentHoldingItemStack = player.inventory.GetItemStackAt(player.SelectedSlotIndex);
                if (player.CurrentRaycastHitResult.IsHit)
                {
                    if(!WorldManager.Instance.TryGetDimension(player.DimensionId, out var dim))return;
                    var stateId = dim.GetBlockAt(player.CurrentRaycastHitResult.BlockDimensionCoord);
                    if(!ResourceSystem.Instance.BlockStates.TryGetResourceWithNumberId(stateId, out var def))return;
                    var blockDef = def.Block;
                    var blockId = def.BlockId;
                    if(!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(currentHoldingItemStack.itemId, out var itemDef))return;
                    if (blockDef.HasBlockEntity)
                    {
                        if(!WorldManager.Instance.TryGetBlockEntity(player.DimensionId, player.CurrentRaycastHitResult.BlockDimensionCoord, out BlockEntity blockEntity))
                            return;
                        var evt = new UseItemOnBlockEntity()
                        {
                            entity = player,
                            HoldingItem = currentHoldingItemStack,
                            ItemDef = itemDef,
                            HitBlockCoord = player.CurrentRaycastHitResult.BlockDimensionCoord,
                            HitNormal = player.CurrentRaycastHitResult.Normal,
                            BlockId = blockId,
                            BlockDef = blockDef,
                            blockEntity = blockEntity,
                            BlockEntityDef = blockEntity.Definition,
                        };
                        EventBus.Instance.Publish(evt);
                        result = evt.Result;
                    }
                    else
                    {
                        var evt = new UseItemOnStaticBlock()
                        {
                            entity = player,
                            HoldingItem = currentHoldingItemStack,
                            HitBlockCoord = player.CurrentRaycastHitResult.BlockDimensionCoord,
                            HitNormal = player.CurrentRaycastHitResult.Normal,
                            ItemDef = itemDef,
                            BlockId = blockId,
                            BlockDef = blockDef
                        };
                        EventBus.Instance.Publish(evt);
                        result = evt.Result;
                    }
                    if(result.UseSuccess)currentHoldingItemStack.TryConsumeItem(result.ConsumeAmount);
                }
            }
            else
            {
                if (player.CurrentRaycastHitResult.IsHit)
                {
                    if(!WorldManager.Instance.TryGetDimension(player.DimensionId, out var dim))return;
                    var stateId = dim.GetBlockAt(player.CurrentRaycastHitResult.BlockDimensionCoord);
                    if(!ResourceSystem.Instance.BlockStates.TryGetResourceWithNumberId(stateId, out var def))return;
                    var blockDef = def.Block;
                    var blockId = def.BlockId;
                    if(!blockDef.HasBlockEntity)
                    {
                        var evt = new InteractWithStaticBlock()
                        {
                            entity = player,
                            HitBlockCoord = player.CurrentRaycastHitResult.BlockDimensionCoord,
                            HitNormal = player.CurrentRaycastHitResult.Normal,
                            BlockId = blockId,
                            BlockDef = blockDef
                        };
                        EventBus.Instance.Publish(evt);
                    }
                    else
                    {
                        // The block has a BE: the chunk hosting it is always
                        // enabled (the ray just hit it), so the lookup must succeed.
                        if(!WorldManager.Instance.TryGetBlockEntity(player.DimensionId, player.CurrentRaycastHitResult.BlockDimensionCoord, out BlockEntity blockEntity))
                            return;
                        var evt = new InteractWithBlockEntity()
                        {
                            entity = player,
                            HitBlockCoord = player.CurrentRaycastHitResult.BlockDimensionCoord,
                            HitNormal = player.CurrentRaycastHitResult.Normal,
                            BlockId = blockId,
                            BlockDef = blockDef,
                            blockEntity = blockEntity,
                            BlockEntityDef = blockEntity.Definition
                        };
                        EventBus.Instance.Publish(evt);
                    }
                }
            }
        }
    }
}   