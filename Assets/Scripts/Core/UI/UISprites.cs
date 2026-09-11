using System.Collections.Generic;
using UnityEngine;

// Single-point logical sprite resolution (E7 of
// Docs/可视化UI布局编辑器-实施文档.md): layout/asset data stores logical ids as
// "<modId>:<name>" with the "minecraft:" prefix optional. Vanilla sprites
// live flat in Textures/UI/<name>, other mods in Textures/UI/<modId>/<name>.
// Resolution never throws: a missing mod sprite falls back to the vanilla
// sprite of the same name, then to the caller's fallbackId, and warns once
// per missing id (an empty/null id means "no sprite intended" - silent).
public static class UISprites
{
    private const string VanillaModId = "minecraft";
    private static readonly HashSet<string> warned = new();

    public static Sprite Resolve(string id, string fallbackId = null)
    {
        var sprite = Load(id);
        if(sprite != null)return sprite;
        if(string.IsNullOrEmpty(id))return Load(fallbackId);
        int separator = id.IndexOf(':');
        if(separator > 0 && id.Substring(0, separator) != VanillaModId)
            sprite = Load(id.Substring(separator + 1));
        if(sprite == null)sprite = Load(fallbackId);
        if(warned.Add(id))
            Debug.LogWarning(sprite == null
                ? $"[UISprites] sprite '{id}' not found"
                : $"[UISprites] sprite '{id}' not found; a fallback sprite is used");
        return sprite;
    }

    private static Sprite Load(string id)
        => string.IsNullOrEmpty(id) ? null : Resources.Load<Sprite>(PathOf(id));

    private static string PathOf(string id)
    {
        int separator = id.IndexOf(':');
        if(separator < 0)return $"Textures/UI/{id}";
        string modId = id.Substring(0, separator);
        string name = id.Substring(separator + 1);
        // Vanilla keeps the flat (prefix-less) directory, like the existing
        // Textures/UI assets; mods nest one level deeper by modId.
        return modId == VanillaModId ? $"Textures/UI/{name}" : $"Textures/UI/{modId}/{name}";
    }
}
