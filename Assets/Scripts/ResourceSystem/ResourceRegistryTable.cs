using System.Collections.Generic;
using Unity.VisualScripting;


public class ResourceType
{
    public string modId;
    public string name;

    public string FullName => $"{modId}:{name}";

    public ResourceType DefaultInstance() => null;
}

public class ResourceRegistryTable<T> where T : ResourceType
{
    private Dictionary<ushort, string> numberIdToStringId = new();
    private Dictionary<string, ushort> stringIdToNumberId = new();
    private Dictionary<ushort, T> numberIdToResourceInfo = new();

    private ushort nextNumber = 0;

    public bool Register(T res)
    {
        string fullName = res.FullName;
        if(stringIdToNumberId.ContainsKey(fullName))return false;
        numberIdToStringId[nextNumber] = fullName;
        stringIdToNumberId[fullName] = nextNumber;
        numberIdToResourceInfo[nextNumber] = res;
        nextNumber++;
        return true;
    }

    public bool TryGetResourceWithFullName(string name, out T value)
    {
        if(stringIdToNumberId.TryGetValue(name, out ushort idx))
        {
            value = numberIdToResourceInfo[idx];
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetResourceWithNumberId(ushort index, out T value)
        =>  numberIdToResourceInfo.TryGetValue(index, out value);

    public bool TryGetNumberId(string name, out ushort value)
        => stringIdToNumberId.TryGetValue(name, out value);
    
    public bool TruGetStringId(ushort index, out string value)
        => numberIdToStringId.TryGetValue(index, out value);
    
    public bool ContainsValue(ushort index)
        =>  numberIdToResourceInfo.ContainsKey(index);
    
    public bool ContainsValue(string name)
        => stringIdToNumberId.ContainsKey(name);
    
    public IEnumerable<T> Values => numberIdToResourceInfo.Values;
}