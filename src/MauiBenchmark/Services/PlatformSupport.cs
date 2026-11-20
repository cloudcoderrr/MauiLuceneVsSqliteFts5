using System.Runtime.InteropServices;
using Benchmarks.Core;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

namespace MauiBenchmark.Services;

/// <summary>
/// Platform glue for reading CI settings, resolving file locations, writing console output,
/// and exiting the process in headless (auto-run) mode.
/// </summary>
public static class PlatformSupport
{
    /// <summary>Populated by the Android MainActivity from launch intent extras (keys include the BENCH_ prefix).</summary>
    public static readonly Dictionary<string, string> LaunchExtras = new(StringComparer.OrdinalIgnoreCase);

    public static string BuildConfiguration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    /// <summary>Reads a BENCH_&lt;key&gt; setting from launch extras (Android) or environment variables.</summary>
    public static string? GetSetting(string key)
    {
        string full = "BENCH_" + key;
        if (LaunchExtras.TryGetValue(full, out var v) && !string.IsNullOrEmpty(v))
        {
            return v;
        }
        return Environment.GetEnvironmentVariable(full);
    }

    public static bool AutoRun
    {
        get
        {
            var v = GetSetting("AUTORUN");
            return v is "1" or "true" or "TRUE" or "yes";
        }
    }

    public static string OutputDirectory => GetSetting("RESULTS_DIR") ?? DefaultDataDir;

    public static string WorkRoot => GetSetting("WORKROOT") ?? Path.Combine(CacheDir, "fts-bench");

    public static string? CollectionDbPath
    {
        get
        {
            var explicitPath = GetSetting("COLLECTION_DB");
            if (!string.IsNullOrEmpty(explicitPath))
            {
                return explicitPath;
            }
            var candidate = Path.Combine(DefaultDataDir, "Collection.db");
            return File.Exists(candidate) ? candidate : null;
        }
    }

    public static string DefaultDataDir
    {
        get
        {
#if ANDROID
            var ext = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
            if (!string.IsNullOrEmpty(ext))
            {
                return ext;
            }
#endif
            return FileSystem.Current.AppDataDirectory;
        }
    }

    public static string CacheDir
    {
        get
        {
            // Internal cache keeps heavy index I/O off the (emulated) external storage.
            return FileSystem.Current.CacheDirectory;
        }
    }

    public static void WriteConsole(string message)
    {
        Console.WriteLine(message);
#if ANDROID
        Android.Util.Log.Info("FTSBENCH", message);
#endif
    }

    public static BenchmarkEnvironment CreateEnvironment()
    {
        var env = new BenchmarkEnvironment
        {
            Platform = DeviceInfo.Current.Platform.ToString(),
            OsVersion = DeviceInfo.Current.VersionString,
            DeviceModel = DeviceInfo.Current.Model,
            DeviceManufacturer = DeviceInfo.Current.Manufacturer,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeDescription = RuntimeInformation.FrameworkDescription,
            BuildConfiguration = BuildConfiguration,
            Is64Bit = Environment.Is64BitProcess,
            ProcessorCount = Environment.ProcessorCount,
        };

        // Normalize platform names for reporting.
        env.Platform = DeviceInfo.Current.Platform == DevicePlatform.Android ? "Android"
            : DeviceInfo.Current.Platform == DevicePlatform.iOS ? "iOS"
            : DeviceInfo.Current.Platform == DevicePlatform.WinUI ? "Windows"
            : DeviceInfo.Current.Platform == DevicePlatform.macOS ? "macOS"
            : DeviceInfo.Current.Platform.ToString();

        return env;
    }

    public static void ExitProcess(int code)
    {
        Console.Out.Flush();
#if ANDROID
        Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
#endif
        Environment.Exit(code);
    }
}
