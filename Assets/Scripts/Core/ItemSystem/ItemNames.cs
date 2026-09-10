using System.Text;

// Derived display names for items (ItemDefinition carries no displayName):
// the registry full name is the single source, so mod items get readable
// labels for free. "minecraft:oak_log" -> "Oak Log".
public static class ItemNames
{
    public static string ModId(string fullName)
    {
        if(string.IsNullOrEmpty(fullName))return "";
        int colon = fullName.IndexOf(':');
        return colon < 0 ? "" : fullName.Substring(0, colon);
    }

    public static string DisplayName(string fullName)
    {
        if(string.IsNullOrEmpty(fullName))return "";
        string name = fullName;
        int colon = name.IndexOf(':');
        if(colon >= 0)name = name.Substring(colon + 1);

        var sb = new StringBuilder(name.Length);
        bool wordStart = true;
        foreach(char c in name)
        {
            if(c == '_')
            {
                sb.Append(' ');
                wordStart = true;
                continue;
            }
            sb.Append(wordStart ? char.ToUpperInvariant(c) : c);
            wordStart = false;
        }
        return sb.ToString();
    }
}
