using System.Collections.Generic;
using UnityEngine;

public partial class Minecraft
{
    // ---- M: UI definitions + input handlers ----
    private void RegisterUisAndInputHandlers()
    {
        ResourceSystem.Instance.UIDefinitions.Register(CrosshairUI.CrosshairUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(HotBarUI.hotbarDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(HeldItemUI.heldItemUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(PlayerInventoryUI.playerInvUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(FurnaceUI.furanceUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(CraftingTableUI.craftingTableUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(PlayerUI.playerUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(WidgetTestUI.widgetTestUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(EntityModelEditorUI.editorUIDefinition);
        ResourceSystem.Instance.UIDefinitions.Register(MenuUI.mainMenuUIDefinition);
        // General hover tooltip (Core layer, D6 of the JEI design doc): lives
        // with the game HUD so item names show even without the JEI mod.
        ResourceSystem.Instance.UIDefinitions.Register(TooltipUI.tooltipUIDefinition);


        ResourceSystem.Instance.InputHandlers.Register(new PlayerInputHandler());
        ResourceSystem.Instance.InputHandlers.Register(new UIInputHandler());
        ResourceSystem.Instance.InputHandlers.Register(new MenuInputHandler());
        // Early-touch the shell manager singleton: since the Attach direct
        // calls became spawn events (Phase C R-C2-3) nothing else instantiates
        // it, and a spawn before its ctor subscribed would lose the shell.
        _ = EntityRenderManager.Instance;
    }

    // ---- N: key bindings ----
    private void RegisterKeyBindings()
    {
        // ---- input actions split by action class (P5): world-interaction
        // actions and UI actions live in separate tables; players rebind via
        // overrides, game code only ever polls action ids, never physical
        // keys. (The JEI three still register on the legacy KeyBindings table
        // until P7.) ----
        void RegisterWorldAction(string name, KeyCode key, string category, params string[] allowedHandlers)
        {
            ResourceSystem.Instance.WorldActions.Register(new WorldAction
            {
                modId = ModId,
                name = name,
                DefaultKey = key,
                Category = category,
                AllowedInputHandlers = allowedHandlers == null ? null : new HashSet<string>(allowedHandlers)
            });
        }
        void RegisterUIAction(string name, KeyCode key, string category, params string[] allowedHandlers)
        {
            ResourceSystem.Instance.UIActions.Register(new UIAction
            {
                modId = ModId,
                name = name,
                DefaultKey = key,
                Category = category,
                AllowedInputHandlers = allowedHandlers == null ? null : new HashSet<string>(allowedHandlers)
            });
        }
        RegisterWorldAction("forward", KeyCode.W, "movement", "minecraft:player_input_handler");
        RegisterWorldAction("back", KeyCode.S, "movement", "minecraft:player_input_handler");
        RegisterWorldAction("left", KeyCode.A, "movement", "minecraft:player_input_handler");
        RegisterWorldAction("right", KeyCode.D, "movement", "minecraft:player_input_handler");
        // Space jumps (MC-style; the ascend/descend flight keys went away with
        // the unified gravity physics, see Docs/受击击退与无敌帧实现方案.md).
        RegisterWorldAction("jump", KeyCode.Space, "movement", "minecraft:player_input_handler");
        RegisterWorldAction("attack", KeyCode.Mouse0, "game", "minecraft:player_input_handler");
        RegisterWorldAction("use_item", KeyCode.Mouse1, "game", "minecraft:player_input_handler");
        // UI actions (game-state triggered, UI-owned; P6 routes them through
        // the UI action ring - global actions, not slot-targeted).
        RegisterUIAction("open_inventory", KeyCode.E, "game", "minecraft:player_input_handler");
        // Same default key as open_inventory on purpose: routing (KeyBindingManager
        // refresh) feeds the press to whichever action the current input context
        // admits, so E opens in game and closes inside a panel (design doc §6.1).
        RegisterUIAction("close_ui", KeyCode.E, "ui", "minecraft:ui_input_handler");
        // Widget smoke-test UI (UI 组件化重构设计方案 §3.4): opens the tab /
        // icon / text / input / button demo panel (WidgetTestUI).
        RegisterUIAction("open_widget_test", KeyCode.T, "game", "minecraft:player_input_handler");
        // Entity model editor (design doc §7): plain P. Decision C originally
        // specified Ctrl+P; the combo collides with the Unity editor's play
        // shortcut, so the revision binds the bare key (2026-09-04).
        RegisterUIAction("open_entity_model_editor", KeyCode.P, "game", "minecraft:player_input_handler");
    }
}
