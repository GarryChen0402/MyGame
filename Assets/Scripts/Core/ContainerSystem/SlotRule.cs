using System.Collections.Generic;
using UnityEngine;

public enum ContainerAccess
{
    Any,
    Player,
    Module,
    None
}

// Per-slot access rule (P3): every slot carries its own insert/extract policy
// - requesters (None = forbidden, Any = either requester) plus a tag whitelist
// per direction (null/empty = unrestricted). Rules are declaration config
// carried by the container's definition, never persisted. Evaluation lives
// here so the container only routes by slot index.
[System.Serializable]
public class SlotRule
{
    public ContainerAccess InsertRequesters = ContainerAccess.Any;
    public ContainerAccess ExtractRequesters = ContainerAccess.Any;
    public List<string> InsertTags;   // whitelist of item tags, e.g. ["fuel"]
    public List<string> ExtractTags;

    public bool CanInsert(ItemStack stack, ContainerAccess requester)
        => Allows(InsertRequesters, requester) && MatchesTags(stack, InsertTags);

    // stack = the item currently in the slot; a tag whitelist makes an
    // empty slot unextractable, which only matters when one is declared.
    public bool CanExtract(ItemStack stack, ContainerAccess requester)
        => Allows(ExtractRequesters, requester) && MatchesTags(stack, ExtractTags);

    private static bool Allows(ContainerAccess requesters, ContainerAccess requester)
        => requesters != ContainerAccess.None
           && (requesters == ContainerAccess.Any || requester == requesters);

    private static bool MatchesTags(ItemStack stack, List<string> tags)
    {
        if (tags == null || tags.Count == 0) return true;
        if (stack == null || stack.IsEmpty()) return false;
        if (!ResourceSystem.Instance.ItemDefinitions.TryGetResourceWithNumberId(stack.itemId, out var def)) return false;
        if (def.Tags == null) return false;
        foreach (string tag in tags)
            if (def.Tags.Contains(tag)) return true;
        return false;
    }
}
