using UnityEngine;

// Runs before WorldRenderer.Update so a block edit marks its chunk rebuild in
// the same frame, letting the Important rebuild dispatch and render immediately.
// [DefaultExecutionOrder(-100)]
public class PlayerInputHandler : IInputHandler
{
    private Player player = null;

    private float mouseSensitivity = 2f;

    private const float RaycastReach = 4.5f;
    public PlayerInputHandler()
    {
        player = Player.Instance;
        modId = "minecraft";
        name = "player_input_handler";
    }
    public override void OnEnter()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    public override void OnUpdate()
    {
        if(player == null)return;
        var keys = KeyBindingManager.Instance;
        // Open the player UI through the command entry (Phase C): the entry
        // runs the open action (2x2 preview refresh) before showing the panel.
        // Closing is handled by UIInputHandler, which owns the input stack
        // while any panel is open - this handler is not updated then, so the
        // action can never double-fire.
        if(keys.WasPressed("minecraft:open_inventory"))
            ContainerCommandProcessor.Instance.OpenPlayerInventory();
        // Widget smoke-test UI (default T).
        if(keys.WasPressed("minecraft:open_widget_test"))
            UIManager.Instance?.OpenUI("minecraft:widget_test");
        // Entity model editor (default P, design doc §7 decision C revised):
        // the Ctrl+P combo collides with the Unity editor's play shortcut.
        if(keys.WasPressed("minecraft:open_entity_model_editor"))
            UIManager.Instance?.OpenUI("entity_model_editor:entity_model_editor");
        MoveHandler(keys);
        InteractionHandler(keys);
    }


    public override void OnExit()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }
    private void MoveHandler(KeyBindingManager keys)
    {
        player.yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        player.pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        player.pitch = Mathf.Clamp(player.pitch, -90f, 90f);

        // Raycast from the eyes toward the crosshair; store the result on the player.
        Vector3 eye = player.Position + Vector3.up * Player.EyeHeight;
        Vector3 dir = Quaternion.Euler(player.pitch, player.yaw, 0) * Vector3.forward;
        RaycastHit hit = default;
        if(WorldManager.Instance.TryGetDimension(player.DimensionId, out Dimension dim))
            hit = Raycaster.Raycast(dim, eye, dir, RaycastReach, out hit, player) ? hit : default;
        player.CurrentRaycastHitResult = hit;
        // if(hit.IsHit)
        //     Debug.Log($"Looking at block {hit.BlockDimensionCoord}, dist {hit.Distance:F2}, normal {hit.Normal}");
        // else
        //     Debug.Log("Looking at air");

        // Hurt window: knockback plays out instead of input writes - no intents
        // are produced and the logic-side gate drops any residual (rule M1
        // equivalence of the former Motion write freeze). Camera/raycast above
        // stay live.
        if(player.InvincibleTimer > 0f)return;

        // Phase B: WASD/jump no longer write Motion directly - they fill the
        // intent slot, consumed by the next game tick (rules M1/B5). The
        // direction synthesis (yaw rotation, grounded gate) moved to
        // Player.ConsumeInputIntent on the logic side.
        Vector2 moveDir = Vector2.zero;
        if(keys.IsDown("minecraft:forward"))moveDir.x += 1;
        if(keys.IsDown("minecraft:back"))moveDir.x -= 1;
        if(keys.IsDown("minecraft:left"))moveDir.y -= 1;
        if(keys.IsDown("minecraft:right"))moveDir.y += 1;

        player.Intent.move = moveDir;
        if(keys.WasPressed("minecraft:jump"))player.Intent.jumpRequested = true;
    }

    private void InteractionHandler(KeyBindingManager keys)
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

        // Left-click (P4 of the item drop dev plan): a mob under the crosshair
        // takes priority over breaking - swing session hits it; otherwise the
        // block break runs (empty-hand or held item both break). The click's
        // target resolves here from the click-frame raycast into an action
        // request; the logic side re-validates distance/existence before any
        // session starts (rules A1/A2) - no direct InteractionManager calls.
        if (keys.WasPressed("minecraft:attack"))
        {
            if (player.CurrentRaycastHitResult.HitEntity is MobEntity mob)
            {
                player.Intent.action = new PlayerActionRequest
                {
                    kind = PlayerActionKind.AttackEntity,
                    entityTarget = mob
                };
            }
            else if (player.CurrentRaycastHitResult.IsHit)
            {
                player.Intent.action = new PlayerActionRequest
                {
                    kind = PlayerActionKind.AttackBlock,
                    blockCoord = player.CurrentRaycastHitResult.BlockDimensionCoord
                };
            }
        }

        if (keys.WasPressed("minecraft:use_item"))
        {
            // Right-click: the click-frame raycast decides block vs air and the
            // held stack rides along - the target coordinate is locked at the
            // click so the session starts against the block the click saw, even
            // if the crosshair drifts before the next tick. Existence/definition
            // checks re-run at consume time on the logic side.
            RaycastHit hit = player.CurrentRaycastHitResult;
            player.Intent.action = new PlayerActionRequest
            {
                kind = PlayerActionKind.Use,
                isBlockHit = hit.IsHit,
                blockCoord = hit.IsHit ? hit.BlockDimensionCoord : default,
                heldStack = player.IsHoldingItem() ? player.GetCurrentHoldingItemStack() : null
            };
        }
    }

}