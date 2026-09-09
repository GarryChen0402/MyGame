using System;

// Slot-level metadata DTO parsed from a world.json v2 file + its folder under
// the saves root. Plain data only - never holds IO state.
public class WorldSlotInfo
{
    public string Folder;           // directory name under the saves root (paths / loading)
    public string Name;             // display name (world.json.name; falls back to Folder)
    public int Seed;
    public DateTime CreatedTime;    // MinValue = unknown
    public DateTime LastPlayedTime; // MinValue = never played
}
