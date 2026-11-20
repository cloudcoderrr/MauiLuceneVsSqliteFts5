namespace Benchmarks.Core;

/// <summary>A source of documents to index. Enumerated lazily so large sets are not materialized.</summary>
public interface IDocumentSource
{
    /// <summary>Human-readable dataset name (e.g. "1,000 records").</summary>
    string Name { get; }

    /// <summary>Total number of documents the source will yield.</summary>
    long Count { get; }

    /// <summary>A term known to exist in the dataset, used for the query benchmark.</summary>
    string QueryTerm { get; }

    /// <summary>Yields the documents. Safe to enumerate multiple times.</summary>
    IEnumerable<SearchDocument> GetDocuments();
}
