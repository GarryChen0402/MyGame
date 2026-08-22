using System.Collections.Generic;


public interface IResourceType {}

public class ResourceRegistryTable<T> where T : IResourceType
{
    private Dictionary<int, string> _numberIdToStringId;
    private Dictionary<string, int> _stringIdToNumberId;
    
    private Dictionary<int, T> _numberIdToResourceINfo;
}