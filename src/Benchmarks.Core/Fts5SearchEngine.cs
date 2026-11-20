using Microsoft.Data.Sqlite;

namespace Benchmarks.Core;

/// <summary>SQLite FTS5 full-text engine. Each index lives in its own database file.</summary>
public sealed class Fts5SearchEngine : ISearchEngine
{
    private readonly string _dbPath;
    private SqliteConnection? _queryConn;

    public Fts5SearchEngine(string workDir)
    {
        SqliteInit.Ensure();
        Directory.CreateDirectory(workDir);
        _dbPath = Path.Combine(workDir, "fts5.db");
    }

    public string Name => "SQLite FTS5";

    private SqliteConnection Open(bool readOnly = false)
    {
        var mode = readOnly ? ";Mode=ReadOnly" : string.Empty;
        var conn = new SqliteConnection($"Data Source={_dbPath}{mode}");
        conn.Open();
        EnglishCollation.Register(conn);
        return conn;
    }

    public long CreateIndex(IDocumentSource source, Action<long>? progress = null)
    {
        foreach (var p in IndexFiles())
        {
            if (File.Exists(p)) File.Delete(p);
        }

        using var conn = Open();
        // Use the default rollback journal (not WAL): the index is built by a single writer and
        // then read back, and a read-only connection cannot reliably read a WAL database on all
        // platforms (notably Android), which produced 0 hits / truncated files there.
        Exec(conn, "PRAGMA synchronous=NORMAL;");
        Exec(conn, "CREATE VIRTUAL TABLE docs USING fts5(headword, content, tokenize='unicode61');");

        long count = 0;
        using (var tx = conn.BeginTransaction())
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO docs(rowid, headword, content) VALUES ($id, $h, $c)";
            var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; cmd.Parameters.Add(pId);
            var pH = cmd.CreateParameter(); pH.ParameterName = "$h"; cmd.Parameters.Add(pH);
            var pC = cmd.CreateParameter(); pC.ParameterName = "$c"; cmd.Parameters.Add(pC);

            foreach (var d in source.GetDocuments())
            {
                pId.Value = d.Id;
                pH.Value = d.Headword ?? string.Empty;
                pC.Value = d.Content ?? string.Empty;
                cmd.ExecuteNonQuery();
                count++;
                if (progress is not null && (count % 20000) == 0)
                {
                    progress(count);
                }
            }

            tx.Commit();
        }

        return count;
    }

    public long GetIndexSizeBytes()
    {
        long total = 0;
        foreach (var p in IndexFiles())
        {
            if (File.Exists(p))
            {
                total += new FileInfo(p).Length;
            }
        }
        return total;
    }

    public void PrepareQuery() => _queryConn = Open(readOnly: true);

    public long CountHits(string term)
    {
        var conn = _queryConn ?? throw new InvalidOperationException("Call PrepareQuery first.");
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM docs WHERE docs MATCH $q";
        cmd.Parameters.AddWithValue("$q", term);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public int RunQuery(string term, int topN)
    {
        var conn = _queryConn ?? throw new InvalidOperationException("Call PrepareQuery first.");
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT rowid FROM docs WHERE docs MATCH $q ORDER BY rank LIMIT $n";
        cmd.Parameters.AddWithValue("$q", term);
        cmd.Parameters.AddWithValue("$n", topN);
        int rows = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            rows++;
        }
        return rows;
    }

    public void Insert(IReadOnlyList<SearchDocument> documents)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO docs(rowid, headword, content) VALUES ($id, $h, $c)";
        var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; cmd.Parameters.Add(pId);
        var pH = cmd.CreateParameter(); pH.ParameterName = "$h"; cmd.Parameters.Add(pH);
        var pC = cmd.CreateParameter(); pC.ParameterName = "$c"; cmd.Parameters.Add(pC);

        foreach (var d in documents)
        {
            pId.Value = d.Id;
            pH.Value = d.Headword ?? string.Empty;
            pC.Value = d.Content ?? string.Empty;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private IEnumerable<string> IndexFiles()
    {
        yield return _dbPath;
        yield return _dbPath + "-wal";
        yield return _dbPath + "-shm";
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _queryConn?.Dispose();
        _queryConn = null;
        SqliteConnection.ClearAllPools();
    }
}
