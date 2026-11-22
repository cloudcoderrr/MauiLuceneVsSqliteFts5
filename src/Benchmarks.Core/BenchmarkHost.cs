using System.Runtime.InteropServices;

namespace Benchmarks.Core;

/// <summary>
/// Shared helpers for building a <see cref="BenchmarkConfig"/> from key/value settings and for
/// writing results. Used by both the console runner and the MAUI app so behaviour stays identical.
/// </summary>
public static class BenchmarkHost
{
    public const string ResultsBeginMarker = "===BENCH_RESULTS_BEGIN===";
    public const string ResultsEndMarker = "===BENCH_RESULTS_END===";
    public const string MarkdownBeginMarker = "===BENCH_MD_BEGIN===";
    public const string MarkdownEndMarker = "===BENCH_MD_END===";
    public const string DoneMarker = "===BENCH_DONE===";
    public const string ErrorMarker = "===BENCH_ERROR===";

    public const string ResultsJsonFileName = "bench-results.json";
    public const string ResultsMarkdownFileName = "bench-results.md";
    public const string LogFileName = "bench-log.txt";

    /// <summary>Builds a config from a settings accessor. Keys are looked up without the BENCH_ prefix.</summary>
    public static BenchmarkConfig BuildConfig(Func<string, string?> get)
    {
        var config = new BenchmarkConfig();

        var datasets = Split(get("DATASETS"));
        if (datasets.Count > 0)
        {
            config.Run1000 = datasets.Contains("1000");
            config.Run1Million = datasets.Contains("1000000") || datasets.Contains("1m");
            config.RunCollection = datasets.Contains("collection") || datasets.Contains("collectiondb");
        }

        var engines = Split(get("ENGINES"));
        if (engines.Count > 0)
        {
            config.UseLucene = engines.Contains("lucene");
            config.UseFts5 = engines.Contains("fts5");
            config.UseSqliteIndex = engines.Contains("index") || engines.Contains("sqliteindex");
        }

        var collection = get("COLLECTION_DB");
        if (!string.IsNullOrWhiteSpace(collection))
        {
            config.CollectionDbPath = collection;
        }

        var work = get("WORKROOT");
        if (!string.IsNullOrWhiteSpace(work))
        {
            config.WorkRoot = work;
        }

        if (TryInt(get("THOUSAND_COUNT"), out var thousand)) config.ThousandCount = thousand;
        if (TryLong(get("MILLION_COUNT"), out var million)) config.MillionCount = million;
        if (TryInt(get("INSERT_COUNT"), out var insert)) config.InsertCount = insert;
        if (TryInt(get("QUERY_TOPN"), out var topN)) config.QueryTopN = topN;
        if (TryInt(get("QUERY_WARMUP"), out var warmup)) config.QueryWarmupRounds = warmup;
        if (TryInt(get("QUERY_REPEAT"), out var repeat)) config.QueryRepeat = repeat;

        return config;
    }

    /// <summary>Creates a default environment using the current runtime; callers can override fields.</summary>
    public static BenchmarkEnvironment DefaultEnvironment(string platform, string buildConfiguration)
    {
        return new BenchmarkEnvironment
        {
            Platform = platform,
            OsVersion = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeDescription = RuntimeInformation.FrameworkDescription,
            BuildConfiguration = buildConfiguration,
            Is64Bit = Environment.Is64BitProcess,
            ProcessorCount = Environment.ProcessorCount,
        };
    }

    /// <summary>Writes results.json, results.md and emits console markers wrapping the JSON.</summary>
    public static void WriteOutputs(BenchmarkReport report, string outputDir, Action<string> console)
    {
        string json = BenchmarkRunner.ToJson(report);
        string markdown = BenchmarkRunner.ToMarkdown(report);

        try
        {
            Directory.CreateDirectory(outputDir);
            File.WriteAllText(Path.Combine(outputDir, ResultsJsonFileName), json);
            File.WriteAllText(Path.Combine(outputDir, ResultsMarkdownFileName), markdown);
        }
        catch (Exception ex)
        {
            console($"[warn] could not write results files to '{outputDir}': {ex.Message}");
        }

        console(string.Empty);
        console(MarkdownBeginMarker);
        console(markdown);
        console(MarkdownEndMarker);
        console(ResultsBeginMarker);
        console(json);
        console(ResultsEndMarker);
        console(DoneMarker);
    }

    private static HashSet<string> Split(string? value)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return set;
        }
        foreach (var part in value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            set.Add(part.Trim());
        }
        return set;
    }

    private static bool TryInt(string? s, out int value) => int.TryParse(s, out value);

    private static bool TryLong(string? s, out long value) => long.TryParse(s, out value);
}
