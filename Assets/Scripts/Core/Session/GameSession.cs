using UnityEngine.SceneManagement;

// Static cross-scene handover context: the main menu picks a slot, the game
// scene's entry controller consumes it. Null/empty means no menu involvement
// (direct play of the game scene is the debug path).
public static class GameSession
{
    public static string ActiveSlotFolder;

    public static void RequestEnterWorld(string folder)
    {
        ActiveSlotFolder = folder;
        // Scene name must stay in sync with Build Settings index 1.
        SceneManager.LoadScene("SampleScene");
    }
}
