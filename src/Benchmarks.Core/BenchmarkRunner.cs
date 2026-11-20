using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Benchmarks.Core;

/// <summary>Runs every (engine, dataset) combination and produces a report.</summary>
public sealed class BenchmarkRunner
{
    private readonly BenchmarkConfig _config;
    private readonly Action<string>? _log;

    public BenchmarkRunner(BenchmarkConfig config, Action<string>? log = null)
    {
        _config = config;
        _log = log;
    }

    public BenchmarkReport Run(BenchmarkEnvironment environment, CancellationToken cancellationToken = default)
    {
        SqliteInit.Ensure();

        var report = new BenchmarkReport
        {
            Environment = environment,
            StartedUtc = DateTime.UtcNow,
        };

        Directory.CreateDirectory(_config.WorkRoot);

        foreach (var dataset in BuildDatasets())
        {
            foreach (var engineName in EngineNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = RunOne(engineName, dataset, cancellationToken);
                report.Entries.Add(entry);
            }
        }

        report.FinishedUtc = DateTime.UtcNow;
        return report;
    }

    private IEnumerable<string> EngineNames()
    {
        if (_config.UseLucene) yield return "Lucene.NET";
        if (_config.UseFts5) yield return "SQLite FTS5";
    }

    private List<DatasetSpec> BuildDatasets()
    {
        var list = new List<DatasetSpec>();
        if (_config.Run1000)
        {
            list.Add(new DatasetSpec("1,000 records",
                () => new SyntheticDataSource(_config.ThousandCount) { Name = "1,000 records" }));
        }
        if (_config.Run1Million)
        {
            list.Add(new DatasetSpec("1,000,000 records",
                () => new SyntheticDataSource(_config.MillionCount) { Name = "1,000,000 records" }));
        }
        if (_config.RunCollection && !string.IsNullOrEmpty(_config.CollectionDbPath) && File.Exists(_config.CollectionDbPath))
        {
            list.Add(new DatasetSpec("Collection.db",
                () => new CollectionDbDataSource(_config.CollectionDbPath!)));
        }
        else if (_config.RunCollection)
        {
            _log?.Invoke($"[skip] Collection.db not found at '{_config.CollectionDbPath}'.");
        }
        return list;
    }

    private BenchmarkEntry RunOne(string engineName, DatasetSpec dataset, CancellationToken ct)
    {
        var entry = new BenchmarkEntry { Engine = engineName, Dataset = dataset.Name };
        string workDir = Path.Combine(_config.WorkRoot, Sanitize(engineName) + "__" + Sanitize(dataset.Name));
        SafeDelete(workDir);
        Directory.CreateDirectory(workDir);

        ISearchEngine? engine = null;
        try
        {
            var source = dataset.Factory();
            entry.QueryTerm = source.QueryTerm;
            engine = CreateEngine(engineName, workDir);

            _log?.Invoke($"[{engineName}] {dataset.Name}: building index...");
            var sw = Stopwatch.StartNew();
            long indexed = engine.CreateIndex(source, n => _log?.Invoke($"[{engineName}] {dataset.Name}: {n:N0} indexed"));
            sw.Stop();
            entry.CreateIndexMs = sw.Elapsed.TotalMilliseconds;
            entry.RecordCount = indexed;
            _log?.Invoke($"[{engineName}] {dataset.Name}: indexed {indexed:N0} in {entry.CreateIndexMs:N0} ms");

            entry.StorageBytes = engine.GetIndexSizeBytes();
            _log?.Invoke($"[{engineName}] {dataset.Name}: storage {FormatBytes(entry.StorageBytes)}");

            ct.ThrowIfCancellationRequested();

            // Query (average of several runs after a warmup).
            engine.PrepareQuery();
            entry.QueryHits = engine.CountHits(source.QueryTerm);
            engine.RunQuery(source.QueryTerm, _config.QueryTopN); // warmup
            var qsw = Stopwatch.StartNew();
            for (int i = 0; i < _config.QueryRepeat; i++)
            {
                engine.RunQuery(source.QueryTerm, _config.QueryTopN);
            }
            qsw.Stop();
            entry.QueryMs = qsw.Elapsed.TotalMilliseconds / Math.Max(1, _config.QueryRepeat);
            _log?.Invoke($"[{engineName}] {dataset.Name}: query '{source.QueryTerm}' {entry.QueryMs:N2} ms avg, {entry.QueryHits:N0} hits");

            ct.ThrowIfCancellationRequested();

            // Insert new documents into the existing index.
            var batch = SyntheticDataSource.GenerateBatch(_config.InsertCount, _config.InsertStartId);
            var isw = Stopwatch.StartNew();
            engine.Insert(batch);
            isw.Stop();
            entry.InsertCount = batch.Count;
            entry.InsertTotalMs = isw.Elapsed.TotalMilliseconds;
            entry.InsertAvgMs = isw.Elapsed.TotalMilliseconds / Math.Max(1, batch.Count);
            _log?.Invoke($"[{engineName}] {dataset.Name}: inserted {batch.Count:N0} in {entry.InsertTotalMs:N0} ms ({entry.InsertAvgMs:N3} ms/doc)");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            entry.Error = ex.Message;
            _log?.Invoke($"[{engineName}] {dataset.Name}: ERROR {ex}");
        }
        finally
        {
            engine?.Dispose();
            SafeDelete(workDir);
        }

        return entry;
    }

    private ISearchEngine CreateEngine(string engineName, string workDir) => engineName switch
    {
        "Lucene.NET" => new LuceneSearchEngine(workDir),
        "SQLite FTS5" => new Fts5SearchEngine(workDir),
        _ => throw new ArgumentOutOfRangeException(nameof(engineName), engineName, "Unknown engine"),
    };

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }
        return sb.ToString();
    }

    private static void SafeDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort cleanup.
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }

    public static string ToJson(BenchmarkReport report) =>
        JsonSerializer.Serialize(report, BenchmarkJsonContext.Default.BenchmarkReport);

    public static string ToMarkdown(BenchmarkReport report)
    {
        var env = report.Environment;
        var sb = new StringBuilder();
        sb.AppendLine($"# FTS Benchmark — {env.Platform}");
        sb.AppendLine();
        sb.AppendLine($"- Device: {env.DeviceManufacturer} {env.DeviceModel}".TrimEnd());
        sb.AppendLine($"- OS: {env.Platform} {env.OsVersion}");
        sb.AppendLine($"- Architecture: {env.Architecture} ({(env.Is64Bit ? "64-bit" : "32-bit")}), {env.ProcessorCount} CPUs");
        sb.AppendLine($"- Runtime: {env.RuntimeDescription}");
        sb.AppendLine($"- Build: {env.BuildConfiguration}");
        sb.AppendLine($"- Started (UTC): {report.StartedUtc:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- Duration: {(report.FinishedUtc - report.StartedUtc).TotalSeconds:N0} s");
        sb.AppendLine();
        sb.AppendLine("| Engine | Dataset | Records | Create index | Query (avg) | Hits | Insert total | Insert avg | Storage |");
        sb.AppendLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var e in report.Entries)
        {
            if (e.Error is not null)
            {
                sb.AppendLine($"| {e.Engine} | {e.Dataset} | - | ERROR: {e.Error} | | | | | |");
                continue;
            }
            sb.AppendLine(
                $"| {e.Engine} | {e.Dataset} | {e.RecordCount:N0} | {e.CreateIndexMs:N0} ms | {e.QueryMs:N2} ms | {e.QueryHits:N0} | {e.InsertTotalMs:N0} ms | {e.InsertAvgMs:N3} ms | {FormatBytes(e.StorageBytes)} |");
        }
        return sb.ToString();
    }

    private sealed record DatasetSpec(string Name, Func<IDocumentSource> Factory);
}
