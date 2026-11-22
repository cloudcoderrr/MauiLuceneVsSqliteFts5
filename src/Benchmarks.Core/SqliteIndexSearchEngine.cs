using Microsoft.Data.Sqlite;

namespace Benchmarks.Core;

/// <summary>
/// A plain SQLite table with a B-tree index on the headword column, queried by exact match
/// (no LIKE). Represents the baseline cost of an indexed equality lookup.
/// </summary>
public sealed class SqliteIndexSearchEngine : ISearchEngine
{
    private readonly string _dbPath;
    private SqliteConnection? _queryConn;
    private string _sampleHeadword = string.Empty;

    public SqliteIndexSearchEngine(string workDir)
    {
        SqliteInit.Ensure();
        Directory.CreateDirectory(workDir);
        _dbPath = Path.Combine(workDir, "index.db");
    }

    public string Name => "SQLite Index";

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
        Exec(conn, "PRAGMA synchronous=NORMAL;");
        Exec(conn, "CREATE TABLE docs (id INTEGER PRIMARY KEY, headword TEXT, content TEXT);");

        long count = 0;
        using (var tx = conn.BeginTransaction())
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO docs(id, headword, content) VALUES ($id, $h, $c)";
            var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; cmd.Parameters.Add(pId);
            var pH = cmd.CreateParameter(); pH.ParameterName = "$h"; cmd.Parameters.Add(pH);
            var pC = cmd.CreateParameter(); pC.ParameterName = "$c"; cmd.Parameters.Add(pC);

            foreach (var d in source.GetDocuments())
            {
                var head = d.Headword ?? string.Empty;
                pId.Value = d.Id;
                pH.Value = head;
                pC.Value = d.Content ?? string.Empty;
                cmd.ExecuteNonQuery();
                count++;
                // Remember a real headword to use for the exact-match query.
                if (head.Length > 0 && (_sampleHeadword.Length == 0 || count == 500))
                {
                    _sampleHeadword = head;
                }
                if (progress is not null && (count % 20000) == 0)
                {
                    progress(count);
                }
            }

            tx.Commit();
        }

        // Build the B-tree index after bulk load (faster than maintaining it per insert).
        Exec(conn, "CREATE INDEX idx_docs_headword ON docs(headword);");
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

    public IReadOnlyList<EngineQuery> GetQueries(IDocumentSource source)
    {
        var conn = _queryConn ?? throw new InvalidOperationException("Call PrepareQuery first.");
        string term = _sampleHeadword;
        return new List<EngineQuery>
        {
            new EngineQuery("Exact match", () => CountExact(conn, term), topN => RunExact(conn, term, topN)),
        };
    }

    private static long CountExact(SqliteConnection conn, string term)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM docs WHERE headword = $h";
        cmd.Parameters.AddWithValue("$h", term);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static int RunExact(SqliteConnection conn, string term, int topN)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM docs WHERE headword = $h LIMIT $n";
        cmd.Parameters.AddWithValue("$h", term);
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
        cmd.CommandText = "INSERT INTO docs(id, headword, content) VALUES ($id, $h, $c)";
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
