// Outbound snapshot of the logic-side ChatLog (D6): value-only arrays, copied
// whole on version change. Read side is the chat panel; nothing else consumes it.
public class ChatLogMirror
{
    public int Version = -1;
    public string[] Lines;
    public bool[] Errors;

    public void ApplyFrom(ChatLog source)
    {
        if(source == null || Version == source.Version)return;
        Version = source.Version;
        int count = source.Count;
        if(Lines == null || Lines.Length != count)
        {
            Lines = new string[count];
            Errors = new bool[count];
        }
        for(int i = 0; i < count; i++)
        {
            var entry = source.GetAt(i);
            Lines[i] = entry.Line;
            Errors[i] = entry.IsError;
        }
    }
}
