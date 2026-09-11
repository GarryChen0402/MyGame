using System;
using System.Collections.Generic;
using UnityEngine;

// Independent item-browser mod (design: Docs/JEI式物品浏览器-功能调研与设计草案.md).
// P1: follow-panel item list, live search and cheat-mode give, on its own
// overlay root. The base game knows nothing about it - everything it needs is
// reached through Core seams (UIActions registry, UIManager.CreateOverlayRoot,
// ContainerCommandProcessor.GiveItem).
public class JEIMod : IMod
{
    public string ModId => "jei";

    // After the base game (Minecraft = 0): OrderBy is a stable sort, so equal
    // priorities keep discovery order and a higher one loads strictly later.
    public int LoadPriority => 1;

    // Built after bootstrap; the ui_action callbacks route through it. Null
    // before that (and on failed boot) - the callbacks guard.
    private JEIPanel panel;

    public void RegisterAllResources()
    {
        // P7: all three are ui_actions now - the UIManager action ring owns
        // polling and routing (this mod never polls its own keys anymore).
        // Whitelisted for both in-game and panel context; the handler stack top
        // is unique, so one physical press can never fire in both contexts.
        void Register(string name, KeyCode key, KeyModifier modifier, bool slotTargeted, Action<UIActionContext> callback)
        {
            ResourceSystem.Instance.UIActions.Register(new UIAction
            {
                modId = ModId,
                name = name,
                DefaultKey = key,
                DefaultModifier = modifier,
                Category = "jei",
                SlotTargeted = slotTargeted,
                Callback = callback,
                AllowedInputHandlers = new HashSet<string>
                {
                    "minecraft:player_input_handler",
                    "minecraft:ui_input_handler"
                }
            });
        }

        // jei:toggle - "follow panels" master switch (default on); global
        // action, never enters the slot gate.
        Register("toggle", KeyCode.O, KeyModifier.Ctrl, false, _ => panel?.ToggleFollow());
        // P2 recipe view (design §6.4): R = how it is made, U = what it is
        // used in; both act on whatever item currently has hover. Slot-
        // targeted: the ring gates them against the hovered slot's actionIds.
        Register("open_usage_ui", KeyCode.R, KeyModifier.None, true, _ => panel?.OnRecipeKey(true));
        Register("open_gain_ui", KeyCode.U, KeyModifier.None, true, _ => panel?.OnRecipeKey(false));
        // Injection (design §2.6): in the PreFreeze window all mods have
        // registered and the tables are still writable - append both ids to
        // every container UI's slot entries. JEI absent = nobody appends =
        // slots naturally do not respond to R/U (zero special cases).
        EventBus.Instance.Subscribe<ResourceRegisterBeforeFreezeEvent>(OnRegisterBeforeFreeze);
        EventBus.Instance.Subscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
    }

    private void OnRegisterBeforeFreeze(ResourceRegisterBeforeFreezeEvent evt)
    {
        EventBus.Instance.Unsubscribe<ResourceRegisterBeforeFreezeEvent>(OnRegisterBeforeFreeze);
        foreach(var def in ResourceSystem.Instance.UIDefinitions.Values)
        {
            if(def.Panel == null)continue;   // non-container UI: no slot entries
            foreach(var entry in def.Panel.Bindings.Values)
            {
                entry.ActionIds.Add("jei:open_usage_ui");
                entry.ActionIds.Add("jei:open_gain_ui");
            }
        }
    }

    // The overlay root is built after bootstrap, not during registration: the
    // item registry and the UI layer are only usable from here on, and the
    // engine's canvas host is guaranteed to exist.
    private void OnBootstrapCompleted(BootstrapCompletedEvent evt)
    {
        EventBus.Instance.Unsubscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
        if(!evt.Success || UIManager.Instance == null)return;
        var root = UIManager.Instance.CreateOverlayRoot("JEIRoot");
        panel = root.AddComponent<JEIPanel>();
    }
}
