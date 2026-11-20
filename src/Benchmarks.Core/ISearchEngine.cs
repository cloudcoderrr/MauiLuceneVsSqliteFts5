namespace Benchmarks.Core;

/// <summary>Abstraction over a full-text search engine for benchmarking.</summary>
public interface ISearchEngine : IDisposable
{
    string Name { get; }

    /// <summary>Builds a fresh index from the source. Returns the number of documents indexed.</summary>
    long CreateIndex(IDocumentSource source, Action<long>? progress = null);

    /// <summary>Total on-disk size of the index in bytes.</summary>
    long GetIndexSizeBytes();

    /// <summary>Opens resources needed for querying (not timed).</summary>
    void PrepareQuery();

    /// <summary>Counts total matches for a term (not timed, used for context).</summary>
    long CountHits(string term);

    /// <summary>Runs a single query returning up to <paramref name="topN"/> rows. Returns rows read.</summary>
    int RunQuery(string term, int topN);

    /// <summary>Inserts new documents one-by-one into the existing index.</summary>
    void Insert(IReadOnlyList<SearchDocument> documents);
}
