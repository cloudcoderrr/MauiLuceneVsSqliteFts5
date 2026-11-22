namespace Benchmarks.Core;

/// <summary>A named query an engine can run, with hit-count and top-N execution.</summary>
public sealed class EngineQuery
{
    private readonly Func<long> _countHits;
    private readonly Func<int, int> _run;

    public EngineQuery(string name, Func<long> countHits, Func<int, int> run)
    {
        Name = name;
        _countHits = countHits;
        _run = run;
    }

    public string Name { get; }

    /// <summary>Total number of matches (not timed, used for context).</summary>
    public long CountHits() => _countHits();

    /// <summary>Runs the query returning up to <paramref name="topN"/> rows. Returns rows read.</summary>
    public int Run(int topN) => _run(topN);
}

/// <summary>Abstraction over a full-text / index engine for benchmarking.</summary>
public interface ISearchEngine : IDisposable
{
    string Name { get; }

    /// <summary>Builds a fresh index from the source. Returns the number of documents indexed.</summary>
    long CreateIndex(IDocumentSource source, Action<long>? progress = null);

    /// <summary>Total on-disk size of the index in bytes.</summary>
    long GetIndexSizeBytes();

    /// <summary>Opens resources needed for querying (not timed).</summary>
    void PrepareQuery();

    /// <summary>The query types this engine runs for the given dataset.</summary>
    IReadOnlyList<EngineQuery> GetQueries(IDocumentSource source);

    /// <summary>Inserts new documents one-by-one into the existing index.</summary>
    void Insert(IReadOnlyList<SearchDocument> documents);
}
