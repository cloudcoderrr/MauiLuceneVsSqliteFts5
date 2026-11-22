using System.Text.Json.Serialization;

namespace Benchmarks.Core;

/// <summary>A single document to be indexed/searched.</summary>
public sealed class SearchDocument
{
    public long Id { get; set; }
    public string Headword { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

/// <summary>Describes the device/runtime a benchmark ran on.</summary>
public sealed class BenchmarkEnvironment
{
    public string Platform { get; set; } = "Unknown";
    public string OsVersion { get; set; } = string.Empty;
    public string DeviceModel { get; set; } = string.Empty;
    public string DeviceManufacturer { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string RuntimeDescription { get; set; } = string.Empty;
    public string BuildConfiguration { get; set; } = string.Empty;
    public bool Is64Bit { get; set; }
    public int ProcessorCount { get; set; }
}

/// <summary>Result of one (engine, dataset) measurement.</summary>
public sealed class BenchmarkEntry
{
    public string Engine { get; set; } = string.Empty;
    public string Dataset { get; set; } = string.Empty;
    public long RecordCount { get; set; }

    public double CreateIndexMs { get; set; }
    public long StorageBytes { get; set; }

    public double InsertTotalMs { get; set; }
    public int InsertCount { get; set; }
    public double InsertAvgMs { get; set; }

    /// <summary>One entry per query type the engine ran (Simple, Fuzzy, Wildcard, ...).</summary>
    public List<QueryResult> Queries { get; set; } = new();

    public string? Error { get; set; }
}

/// <summary>Timing for a single query type.</summary>
public sealed class QueryResult
{
    public string Name { get; set; } = string.Empty;
    public double Ms { get; set; }
    public long Hits { get; set; }
    public string? Error { get; set; }
}

/// <summary>Full report for one platform run.</summary>
public sealed class BenchmarkReport
{
    public BenchmarkEnvironment Environment { get; set; } = new();
    public DateTime StartedUtc { get; set; }
    public DateTime FinishedUtc { get; set; }
    public int QueryTopN { get; set; }
    public int QueryWarmupRounds { get; set; }
    public int QueryTimedRounds { get; set; }
    public List<BenchmarkEntry> Entries { get; set; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BenchmarkReport))]
[JsonSerializable(typeof(BenchmarkEntry))]
[JsonSerializable(typeof(QueryResult))]
[JsonSerializable(typeof(BenchmarkEnvironment))]
internal partial class BenchmarkJsonContext : JsonSerializerContext
{
}
