using System.Collections.Generic;
using UnityEngine;

// Independent item-browser mod (design: Docs/JEI式物品浏览器-功能调研与设计草案.md).
// P1: follow-panel item list, live search and cheat-mode give, on its own
// overlay root. The base game knows nothing about it - everything it needs is
// reached through Core seams (KeyBindings registry, UIManager.CreateOverlayRoot,
// ContainerCommandProcessor.GiveItem).
public class JEIMod : IMod
{
    public string ModId => "jei";

    // After the base game (Minecraft = 0): OrderBy is a stable sort, so equal
    // priorities keep discovery order and a higher one loads strictly later.
    public int LoadPriority => 1;

    public void RegisterAllResources()
    {
        // Whitelisted for both in-game and panel context; the handler stack top
        // is unique, so one physical press can never fire in both contexts.
        void Register(string name, KeyCode key, KeyModifier modifier)
        {
            ResourceSystem.Instance.KeyBindings.Register(new KeyBinding
            {
                modId = ModId,
                name = name,
                DefaultKey = key,
                DefaultModifier = modifier,
                Category = "jei",
                AllowedInputHandlers = new HashSet<string>
                {
                    "minecraft:player_input_handler",
                    "minecraft:ui_input_handler"
                }
            });
        }

        // jei:toggle - "follow panels" master switch (default on).
        Register("toggle", KeyCode.O, KeyModifier.Ctrl);
        // P2 recipe view (design §6.4): R = how it is made, U = what it is
        // used in; both act on whatever item currently has hover.
        Register("show_recipes", KeyCode.R, KeyModifier.None);
        Register("show_uses", KeyCode.U, KeyModifier.None);
        EventBus.Instance.Subscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
    }

    // The overlay root is built after bootstrap, not during registration: the
    // item registry and the UI layer are only usable from here on, and the
    // engine's canvas host is guaranteed to exist.
    private void OnBootstrapCompleted(BootstrapCompletedEvent evt)
    {
        EventBus.Instance.Unsubscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
        if(!evt.Success || UIManager.Instance == null)return;
        var root = UIManager.Instance.CreateOverlayRoot("JEIRoot");
        root.AddComponent<JEIPanel>();
    }
}
