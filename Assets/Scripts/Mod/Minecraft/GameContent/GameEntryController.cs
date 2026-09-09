using UnityEngine;

// Mounted on SampleScene's TestBlock (kept from the WorldSystemTester era;
// file and class names stay to avoid a missing-script on the scene object).
// Single-scene session controller (design §3.1): Start lands in the menu state
// - freeze the logic (Phase6) and open the registered main_menu panel, which
// pushes the menu input handler and unlocks the cursor. EnterWorld is the only
// path from menu to game: close the panel (pops the menu handler, the player
// handler below re-locks), open the gated game HUD, then run the formal
// enter-world sequence (13 steps, design §A.6).
public class GameEntryController : MonoBehaviour
{
    public static GameEntryController Instance { get; private set; }

    private void Start()
    {
        Instance = this;
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
}
