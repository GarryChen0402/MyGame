using System.Collections.Generic;

// Logic-side echo carrier (D6=A of Docs/指令系统-设计草案.md): the dispatcher
// writes, ChatLogMirror copies, the chat panel renders. Process-lifetime
// singleton - history survives panel close/reopen (server-side semantics);
// a future process split turns this into the outbound message stream.
public class ChatLog
{
    public static ChatLog Instance { get; } = new();

    public const int Capacity = 50;

    public readonly struct Entry
    {
        public readonly string Line;
        public readonly bool IsError;
        public Entry(string line, bool isError)
        {
            Line = line;
            IsError = isError;
        }
    }

    private readonly List<Entry> entries = new();

    // Bumped on every Add; the mirror skips unchanged versions (per-frame poll).
    public int Version { get; private set; }

    public int Count => entries.Count;

    public void Add(string line, bool isError)
    {
        if(entries.Count >= Capacity)entries.RemoveAt(0);
        entries.Add(new Entry(line ?? string.Empty, isError));
        Version++;
    }

    public Entry GetAt(int index) => entries[index];
}
