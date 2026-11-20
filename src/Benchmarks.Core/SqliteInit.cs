namespace Benchmarks.Core;

/// <summary>Ensures the native SQLite provider (e_sqlite3, FTS5-enabled) is initialized once.</summary>
public static class SqliteInit
{
    private static int _initialized;

    public static void Ensure()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
        {
            SQLitePCL.Batteries_V2.Init();
        }
    }
}
