
using System.Collections.Generic;

public class WorldManager 
{
    public static WorldManager Instance {get;} = new();

    public readonly Dictionary<ushort, Dimension> Dimensions = new();

    public bool IsDimensionExist(ushort dimensionId) => Dimensions.ContainsKey(dimensionId);

    public bool TryGenerateDimension(string dimensionFullName, out Dimension dimension)
    {
        dimension = null;
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetResourceWithFullName(dimensionFullName, out var def))return false;
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(dimensionFullName, out var id))return false;
        if (IsDimensionExist(id))
        {
            dimension = Dimensions[id];
            return true;
        }
        dimension = new Dimension(def);
        Dimensions[id] = dimension;
        return true;
    }
    
    public bool TryGetDimension(string dimensionName, out Dimension dim)
    {
        dim = null;
        if(!ResourceSystem.Instance.DimensionDefinitions.TryGetNumberId(dimensionName, out var id))return false;
        return TryGetDimension(id, out dim);
    }

    public bool TryGetDimension(ushort dimId, out Dimension dim) => Dimensions.TryGetValue(dimId, out dim);
}