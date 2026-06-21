namespace AMID.Services;

public static class AppLog
{
    private const int MaxEntries = 250;
    private static readonly object SyncRoot = new();
    private static readonly Queue<string> Entries = new();

    public static void Add(string entry)
    {
        lock (SyncRoot)
        {
            Entries.Enqueue(entry);
            while (Entries.Count > MaxEntries)
            {
                Entries.Dequeue();
            }
        }
    }

    public static string GetRecentText()
    {
        lock (SyncRoot)
        {
            return Entries.Count == 0
                ? "(No in-app log entries captured.)"
                : string.Join(Environment.NewLine, Entries.Reverse());
        }
    }
}
