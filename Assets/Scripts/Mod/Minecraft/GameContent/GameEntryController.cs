using UnityEngine;

// Mounted on SampleScene's TestBlock (kept from the WorldSystemTester era;
// file and class names stay to avoid a missing-script on the scene object).
// Single-scene session controller (design §3.1): the session lands in the menu
// state - on bootstrap completion during stepper-driven startup, directly on
// re-play - freezing the logic (Phase6) and opening the registered main_menu
// panel, which pushes the menu input handler and unlocks the cursor. EnterWorld is the only
// path from menu to game: close the panel (pops the menu handler, the player
// handler below re-locks), open the gated game HUD, then run the formal
// enter-world sequence (13 steps, design §A.6).
public class GameEntryController : MonoBehaviour
{
    public static GameEntryController Instance { get; private set; }

    private void Start()
    {
        Instance = this;
        // L1 gate (Part B §5.4): with the stepper-driven startup the mod
        // registries are empty while this scene loads, so the menu must open
        // on the completion event instead of here. The direct path covers
        // IsBootstrapped=true (domain-reload-off re-play); both share OpenMenu.
        if(GameBootstrap.IsBootstrapped)OpenMenu();
        else EventBus.Instance.Subscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
    }

    private void OnBootstrapCompleted(BootstrapCompletedEvent evt)
    {
        EventBus.Instance.Unsubscribe<BootstrapCompletedEvent>(OnBootstrapCompleted);
        OpenMenu();
    }

    private void OpenMenu()
    {
        if(!GameBootstrap.Phase6_MainMenu())return;   // bootstrap incomplete: stay inert
        UIManager.Instance.OpenUI("minecraft:main_menu");
    }

    public void EnterWorld(string folder)
    {
        // CloseUI hides the menu panel and pops the menu input handler.
        UIManager.Instance.CloseUI();
        UIManager.Instance.OpenGameHUD();
        WorldSession.StartEnterWorld(folder);
    }

    // Pause page's "Save and Quit to Title" - the only world -> menu path.
    // CloseUI runs the pause panel's OnDisable (resumes the logic + reopens
    // the HUD); the re-freeze and HUD close land in the SAME synchronous
    // stack, so no tick can interleave and the reopen is never rendered.
    public void ReturnToMainMenu()
    {
        UIManager.Instance.CloseUI();
        GameLoopDriver.PauseLogic = true;
        UIManager.Instance.CloseGameHUD();
        WorldSession.ExitWorld();
        OpenMenu();   // Phase6 is idempotent: re-freezes + opens main_menu (menu handler -> cursor unlock)
    }
}
