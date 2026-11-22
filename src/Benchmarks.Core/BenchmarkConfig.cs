namespace Benchmarks.Core;

/// <summary>Controls which datasets/engines run and the benchmark parameters.</summary>
public sealed class BenchmarkConfig
{
    public bool Run1000 { get; set; } = true;
    public bool Run1Million { get; set; } = true;
    public bool RunCollection { get; set; } = true;

    public bool UseLucene { get; set; } = true;
    public bool UseFts5 { get; set; } = true;
    public bool UseSqliteIndex { get; set; } = true;

    public int ThousandCount { get; set; } = 1_000;
    public long MillionCount { get; set; } = 1_000_000;

    public int InsertCount { get; set; } = 1_000;
    public int QueryTopN { get; set; } = 50;
    public int QueryRepeat { get; set; } = 5;

    /// <summary>Path to the (reassembled) Collection.db, or null to skip the Collection dataset.</summary>
    public string? CollectionDbPath { get; set; }

    /// <summary>Root directory where temporary indexes are built. Cleaned per (engine, dataset).</summary>
    public string WorkRoot { get; set; } = Path.Combine(Path.GetTempPath(), "fts-bench");

    /// <summary>High id offset for inserted documents so they never collide with existing ids.</summary>
    public long InsertStartId { get; set; } = 9_000_000_000L;
}
