using System.Text;
using Microsoft.Data.Sqlite;

namespace Benchmarks.Core;

/// <summary>
/// Reads documents from a Collection.db note table (headword = C1, content = C2).
/// Registers the required <c>collation_en_us</c> collation on every connection.
/// </summary>
public sealed class CollectionDbDataSource : IDocumentSource
{
    private readonly string _dbPath;
    private readonly string _table;
    private long _count = -1;

    public CollectionDbDataSource(string dbPath, string? tableName = null)
    {
        SqliteInit.Ensure();
        _dbPath = dbPath;
        _table = tableName ?? DetectLargestNotesTable(dbPath);
    }

    public string Name => "Collection.db";

    public string TableName => _table;

    public long Count
    {
        get
        {
            if (_count < 0)
            {
                using var conn = Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM [{_table}]";
                _count = Convert.ToInt64(cmd.ExecuteScalar());
            }
            return _count;
        }
    }

    // "container" appears in the HTML wrapper of every dictionary entry (class="container"),
    // and is tokenized by both Lucene StandardAnalyzer and FTS5 unicode61.
    public string QueryTerm => "container";

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
        conn.Open();
        EnglishCollation.Register(conn);
        return conn;
    }

    public IEnumerable<SearchDocument> GetDocuments()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, C1, C2 FROM [{_table}]";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            yield return new SearchDocument
            {
                Id = reader.GetInt64(0),
                Headword = GetText(reader, 1),
                Content = GetText(reader, 2),
            };
        }
    }

    private static string GetText(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => value?.ToString() ?? string.Empty,
        };
    }

    /// <summary>Finds the Notes_* table with the most rows that exposes C1 and C2 columns.</summary>
    public static string DetectLargestNotesTable(string dbPath)
    {
        SqliteInit.Ensure();
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        conn.Open();
        EnglishCollation.Register(conn);

        var candidates = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE 'Notes\\_%' ESCAPE '\\'";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                candidates.Add(reader.GetString(0));
            }
        }

        string best = string.Empty;
        long bestCount = -1;
        foreach (var table in candidates)
        {
            if (!HasColumns(conn, table))
            {
                continue;
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM [{table}]";
            long count = Convert.ToInt64(cmd.ExecuteScalar());
            if (count > bestCount)
            {
                bestCount = count;
                best = table;
            }
        }

        if (best.Length == 0)
        {
            throw new InvalidOperationException($"No suitable Notes_* table found in '{dbPath}'.");
        }

        return best;
    }

    private static bool HasColumns(SqliteConnection conn, string table)
    {
        bool hasC1 = false, hasC2 = false;
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info([{table}])";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (name == "C1") hasC1 = true;
            else if (name == "C2") hasC2 = true;
        }
        return hasC1 && hasC2;
    }
}
