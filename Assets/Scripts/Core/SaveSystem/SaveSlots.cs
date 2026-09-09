using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// Directory-level multi-slot world management under <project>/Saves. Pure
// static helpers over the save layout (no IO state, main thread only): a slot
// is a directory holding a v2 world.json. The legacy single-slot layout
// (files directly under Saves/) is migrated once into "World 1" (design
// §A.3/§A.4.3) so old saves survive the multi-slot switchover.
public static class SaveSlots
{
    public static string SavesRoot => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Saves");

    // One-shot legacy layout migration. Fires only when world.json sits directly
    // under the saves root AND no slot directory exists: the whole legacy layout
    // (player.json, dimension directories) moves into "World 1" ("World 2" on a
    // name conflict) and world.json is rewritten as v2 with backfilled fields
    // (name = the new folder, createdTime = the old file's write time, spawn =
    // engine default). Backfilling treats every root file alike whatever version
    // it claims - SaveWorldMeta may already have written a v2 root file whose new
    // fields are still empty. Idempotent: false when there is nothing to migrate.
    // Slot dirs present while legacy files remain -> warn and skip (no guessing).
    public static bool EnsureMigrated()
    {
        try
        {
            Directory.CreateDirectory(SavesRoot);
            string rootMeta = Path.Combine(SavesRoot, "world.json");
            if(!File.Exists(rootMeta))return false;
            foreach(var dir in Directory.EnumerateDirectories(SavesRoot))
                if(File.Exists(Path.Combine(dir, "world.json")))
                {
                    Debug.LogWarning("[SaveSlots] slot directories exist while legacy files remain under the Saves/ root; migration skipped");
                    return false;
                }

            // Parse the legacy meta before moving anything: the read keeps its
            // write time available even after the file itself has been relocated.
            WorldSaveData legacy = null;
            string created = null;
            try
            {
                created = File.GetLastWriteTimeUtc(rootMeta).ToString("o");
                legacy = JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(rootMeta));
            }
            catch(Exception e) { Debug.LogWarning($"[SaveSlots] legacy world.json unreadable: {e}"); }

            // Collect legacy entries (dimension directories, player.json) first,
            // so the freshly created slot dir is not picked up by the scan.
            var legacyEntries = new List<string>();
            foreach(var entry in Directory.EnumerateFileSystemEntries(SavesRoot))
                if(Path.GetFileName(entry) != "world.json")legacyEntries.Add(entry);

            string folder = UniqueFolderName("World 1");
            string slotDir = Path.Combine(SavesRoot, folder);
            Directory.CreateDirectory(slotDir);
            foreach(var entry in legacyEntries)
            {
                string leaf = Path.GetFileName(entry);
                if(Directory.Exists(entry))Directory.Move(entry, Path.Combine(slotDir, leaf));
                else if(File.Exists(entry))File.Move(entry, Path.Combine(slotDir, leaf));
            }

            created = legacy != null && !string.IsNullOrEmpty(legacy.createdTime) ? legacy.createdTime : created;
            var data = new WorldSaveData
            {
                version = 2,
                seed = legacy != null ? legacy.seed : WorldManager.Instance.Seed,
                name = legacy != null && !string.IsNullOrEmpty(legacy.name) ? legacy.name : folder,
                createdTime = created,
                lastPlayedTime = legacy != null && !string.IsNullOrEmpty(legacy.lastPlayedTime)
                    ? legacy.lastPlayedTime : created,
                spawn = legacy != null && legacy.version >= 2 && legacy.spawn != Vector3.zero
                    ? legacy.spawn : WorldDefaults.DefaultSpawnPosition   // decision B
            };
            ChunkSerializer.WriteFileAtomic(Path.Combine(slotDir, "world.json"), JsonUtility.ToJson(data));
            File.Delete(rootMeta);
            Debug.Log($"[SaveSlots] migrated legacy save layout into '{folder}'");
            return true;
        }
        catch(Exception e)
        {
            Debug.LogError($"[SaveSlots] migration failed: {e}");
            return false;
        }
    }

    // Enumerates the slot directories under the saves root. Entries with an
    // unreadable world.json are skipped with a warning. Order: most recently
    // played first; never-played (unknown) entries sink to the end, then by
    // folder name.
    public static List<WorldSlotInfo> ListSlots()
    {
        var slots = new List<WorldSlotInfo>();
        if(!Directory.Exists(SavesRoot))return slots;
        foreach(var dir in Directory.EnumerateDirectories(SavesRoot))
        {
            string metaPath = Path.Combine(dir, "world.json");
            if(!File.Exists(metaPath))continue;   // not a slot (dimension sub-dir etc.)
            try
            {
                var data = JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(metaPath));
                if(data == null) throw new InvalidDataException("world.json parse error");
                string folder = Path.GetFileName(dir);
                slots.Add(new WorldSlotInfo
                {
                    Folder = folder,
                    Name = string.IsNullOrEmpty(data.name) ? folder : data.name,
                    Seed = data.seed,
                    CreatedTime = ParseTime(data.createdTime),
                    LastPlayedTime = ParseTime(data.lastPlayedTime)
                });
            }
            catch(Exception e)
            {
                Debug.LogWarning($"[SaveSlots] skip unreadable slot '{dir}': {e}");
            }
        }
        slots.Sort((a, b) =>
        {
            int byTime = b.LastPlayedTime.CompareTo(a.LastPlayedTime);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.Folder, b.Folder);
        });
        return slots;
    }

    // Creates a new world slot: folder = SanitizeFolderName(name), uniquified
    // with a " 2" " 3"... suffix when taken; a null seed draws a random nonzero
    // int. Writes a v2 world.json (name = trimmed input, spawn = the engine
    // default, decision B). Returns the slot info, or null on failure.
    public static WorldSlotInfo CreateWorld(string name, int? seed = null)
    {
        try
        {
            string trimmed = name?.Trim() ?? "";
            string folder = UniqueFolderName(SanitizeFolderName(trimmed));
            string slotDir = Path.Combine(SavesRoot, folder);
            Directory.CreateDirectory(slotDir);
            int s = seed ?? new System.Random().Next(1, int.MaxValue);
            DateTime now = DateTime.UtcNow;
            string storedName = trimmed.Length > 0 ? trimmed : folder;
            var data = new WorldSaveData
            {
                version = 2,
                seed = s,
                name = storedName,
                createdTime = now.ToString("o"),
                lastPlayedTime = now.ToString("o"),
                spawn = WorldDefaults.DefaultSpawnPosition
            };
            ChunkSerializer.WriteFileAtomic(Path.Combine(slotDir, "world.json"), JsonUtility.ToJson(data));
            return new WorldSlotInfo
            {
                Folder = folder, Name = storedName, Seed = s,
                CreatedTime = now, LastPlayedTime = now
            };
        }
        catch(Exception e)
        {
            Debug.LogError($"[SaveSlots] create world '{name}' failed: {e}");
            return null;
        }
    }

    // Recursively deletes a whole slot directory. The folder must be a bare
    // directory name (Path.GetFileName self-proof) so no path can escape the
    // saves root.
    public static bool DeleteSlot(string folder)
    {
        if(string.IsNullOrEmpty(folder) || folder != Path.GetFileName(folder))
        {
            Debug.LogError($"[SaveSlots] refuse to delete slot '{folder}' (invalid folder name)");
            return false;
        }
        string slotDir = Path.Combine(SavesRoot, folder);
        if(!Directory.Exists(slotDir))return false;
        try
        {
            Directory.Delete(slotDir, true);
            return true;
        }
        catch(Exception e)
        {
            Debug.LogError($"[SaveSlots] delete slot '{folder}' failed: {e}");
            return false;
        }
    }

    // Folder-safe rendering of a display name: path-hostile characters become
    // '_' (illegal on any of the supported platforms), then trimmed; an empty
    // result falls back to "World".
    public static string SanitizeFolderName(string name)
    {
        if(string.IsNullOrWhiteSpace(name))return "World";
        char[] chars = name.Trim().ToCharArray();
        char[] invalid = Path.GetInvalidFileNameChars();
        for(int i = 0; i < chars.Length; i++)
            if(Array.IndexOf(invalid, chars[i]) >= 0)chars[i] = '_';
        string result = new string(chars);
        return result.Length == 0 ? "World" : result;
    }

    // True when a slot with this folder name exists (UI duplicate-name check).
    public static bool FolderExists(string folder)
    {
        if(string.IsNullOrEmpty(folder))return false;
        return File.Exists(Path.Combine(SavesRoot, folder, "world.json"));
    }

    private static string UniqueFolderName(string baseName)
    {
        string folder = baseName;
        int n = 2;
        while(Directory.Exists(Path.Combine(SavesRoot, folder)))
        {
            folder = $"{baseName} {n}";
            n++;
        }
        return folder;
    }

    private static DateTime ParseTime(string s)
    {
        if(string.IsNullOrEmpty(s))return DateTime.MinValue;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var t)
            ? t : DateTime.MinValue;   // malformed stamp: unknown, never throw
    }
}
