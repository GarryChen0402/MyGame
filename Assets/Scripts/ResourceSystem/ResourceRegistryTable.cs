using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;


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
    private bool frozen;

    // Make the table read-only. Register after freezing is a boot bug (e.g. a
    // texture added after atlas packing would never get UVs), so it fails loudly.
    public void Freeze()
    {
        frozen = true;
        StringBuilder sb = new();
        sb.AppendLine($"[ResourceRegistry] {typeof(T).Name} frozen with {nextNumber} entries:");
        foreach(var res in numberIdToResourceInfo.Values)
            sb.AppendLine($"    {res.FullName}");
        Debug.Log(sb);
    }

    public bool Register(T res)
    {
        if(frozen)
        {
            Debug.LogError($"[ResourceRegistry] {typeof(T).Name} is frozen, rejected: {res.FullName}");
            return false;
        }

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
    
    public bool TryGetStringId(ushort index, out string value)
        => numberIdToStringId.TryGetValue(index, out value);
    
    public bool ContainsValue(ushort index)
        =>  numberIdToResourceInfo.ContainsKey(index);
    
    public bool ContainsValue(string name)
        => stringIdToNumberId.ContainsKey(name);
    
    public IEnumerable<T> Values => numberIdToResourceInfo.Values;
}