using Benchmarks.Core;

namespace MauiBenchmark.Services;

/// <summary>Bridges the MAUI app to <see cref="BenchmarkRunner"/> with platform paths and settings.</summary>
public static class BenchmarkAppRunner
{
    public static BenchmarkConfig BuildConfigFromSettings()
    {
        var config = BenchmarkHost.BuildConfig(PlatformSupport.GetSetting);
        config.WorkRoot = PlatformSupport.WorkRoot;
        config.CollectionDbPath = PlatformSupport.CollectionDbPath;
        return config;
    }

    public static Task<BenchmarkReport> RunAsync(BenchmarkConfig config, Action<string> log, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var environment = PlatformSupport.CreateEnvironment();
            var runner = new BenchmarkRunner(config, log);
            return runner.Run(environment, ct);
        }, ct);
    }

    public static void WriteOutputs(BenchmarkReport report)
    {
        BenchmarkHost.WriteOutputs(report, PlatformSupport.OutputDirectory, PlatformSupport.WriteConsole);
    }

    /// <summary>Headless entry point for CI: run everything, write results, then exit the process.</summary>
    public static async Task RunHeadlessAndExitAsync()
    {
        try
        {
            PlatformSupport.WriteConsole("===BENCH_STARTING===");
            var config = BuildConfigFromSettings();
            PlatformSupport.WriteConsole(
                $"[config] 1000={config.Run1000} 1M={config.Run1Million} collection={config.RunCollection} " +
                $"lucene={config.UseLucene} fts5={config.UseFts5} collectionDb={config.CollectionDbPath ?? "(none)"} " +
                $"out={PlatformSupport.OutputDirectory}");

            var report = await RunAsync(config, PlatformSupport.WriteConsole, CancellationToken.None).ConfigureAwait(false);
            WriteOutputs(report);
            PlatformSupport.ExitProcess(0);
        }
        catch (Exception ex)
        {
            PlatformSupport.WriteConsole(BenchmarkHost.ErrorMarker);
            PlatformSupport.WriteConsole(ex.ToString());
            PlatformSupport.ExitProcess(1);
        }
    }
}
