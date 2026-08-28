using System.Collections.Generic;
using UnityEngine;

public class BlockDefinition : ResourceType
{
    public string ModelId;
    public Dictionary<string, string> TextureIds;
    // false for glass/water/plants: such blocks never hide the faces behind them.
    public bool IsOpaque = true;

    public List<AABB> AABBs = null;
}
