using Benchmarks.Core;

// Console host for desktop runs (used for local validation and the Windows CI job).
// Configuration comes from BENCH_* environment variables; see README.

static string? Env(string key) => Environment.GetEnvironmentVariable("BENCH_" + key);

string platform = Env("PLATFORM") ?? (OperatingSystem.IsWindows() ? "Windows"
    : OperatingSystem.IsMacOS() ? "macOS" : "Linux");
string buildConfig = Env("BUILD_CONFIG") ??
#if DEBUG
    "Debug";
#else
    "Release";
#endif

var config = BenchmarkHost.BuildConfig(Env);
config.WorkRoot = Env("WORKROOT") ?? Path.Combine(Path.GetTempPath(), "fts-bench-console");

string outputDir = Env("RESULTS_DIR") ?? Directory.GetCurrentDirectory();

var environment = BenchmarkHost.DefaultEnvironment(platform, buildConfig);
environment.DeviceModel = Environment.MachineName;
environment.DeviceManufacturer = platform;

Console.WriteLine($"FTS Benchmark console host — {platform} ({buildConfig})");
Console.WriteLine($"Datasets: 1000={config.Run1000} 1M={config.Run1Million} collection={config.RunCollection}");
Console.WriteLine($"Engines: lucene={config.UseLucene} fts5={config.UseFts5}");
Console.WriteLine($"Collection.db: {config.CollectionDbPath ?? "(none)"}");
Console.WriteLine($"Results dir: {outputDir}");
Console.WriteLine();

try
{
    var runner = new BenchmarkRunner(config, msg => Console.WriteLine(msg));
    var report = runner.Run(environment);
    BenchmarkHost.WriteOutputs(report, outputDir, Console.WriteLine);
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine(BenchmarkHost.ErrorMarker);
    Console.WriteLine(ex.ToString());
    return 1;
}
