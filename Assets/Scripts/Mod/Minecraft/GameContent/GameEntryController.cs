using UnityEngine;

// Mounted on SampleScene's TestBlock (replaces WorldSystemTester). Thin shell:
// hands the slot chosen on the main menu to the formal enter-world sequence;
// nothing else is needed from this component afterwards.
public class GameEntryController : MonoBehaviour
{
    private void Start()
    {
        WorldSession.StartEnterWorld(GameSession.ActiveSlotFolder);
    }
}
