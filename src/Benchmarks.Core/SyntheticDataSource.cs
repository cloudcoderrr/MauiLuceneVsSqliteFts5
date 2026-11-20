using System.Text;

namespace Benchmarks.Core;

/// <summary>
/// Deterministically generates English-like documents so runs are reproducible across platforms.
/// Each document's text is seeded by its id, so the set can be regenerated or extended for inserts.
/// </summary>
public sealed class SyntheticDataSource : IDocumentSource
{
    // Non-stopword vocabulary. Index 0 is used as the query term so it appears in a large,
    // predictable fraction of documents (and is NOT a Lucene StandardAnalyzer stopword).
    internal static readonly string[] Vocabulary =
    {
        "system", "network", "memory", "benchmark", "database", "index", "query", "storage",
        "document", "content", "engine", "platform", "device", "mobile", "desktop", "compute",
        "process", "thread", "buffer", "cursor", "record", "column", "table", "schema",
        "search", "match", "token", "analyzer", "collation", "culture", "language", "english",
        "performance", "latency", "throughput", "insert", "update", "delete", "select", "commit",
        "transaction", "journal", "checkpoint", "vacuum", "optimize", "segment", "merge", "flush",
        "cache", "pointer", "vector", "matrix", "kernel", "driver", "socket", "packet",
        "protocol", "service", "request", "response", "payload", "header", "metadata", "binary",
        "encode", "decode", "compress", "archive", "backup", "restore", "migrate", "replicate",
        "cluster", "shard", "partition", "replica", "primary", "secondary", "master", "worker",
        "random", "sequence", "iterator", "generator", "sample", "dataset", "corpus", "feature",
        "measure", "metric", "average", "median", "percentile", "deviation", "variance", "sum",
        "mountain", "river", "forest", "desert", "ocean", "valley", "island", "harbor",
        "morning", "evening", "summer", "winter", "autumn", "spring", "weather", "season",
    };

    private readonly long _count;
    private readonly int _seed;
    private readonly long _startId;

    public SyntheticDataSource(long count, int seed = 20240501, long startId = 1)
    {
        _count = count;
        _seed = seed;
        _startId = startId;
    }

    public string Name { get; init; } = "synthetic";

    public long Count => _count;

    public string QueryTerm => Vocabulary[0];

    public IEnumerable<SearchDocument> GetDocuments()
    {
        for (long i = 0; i < _count; i++)
        {
            long id = _startId + i;
            yield return Create(id, _seed);
        }
    }

    /// <summary>Creates a deterministic document for a given id. Used for both indexing and inserts.</summary>
    public static SearchDocument Create(long id, int seed)
    {
        // Mix id into the seed so each document is independent and reproducible.
        var rnd = new Random(unchecked((int)(id * 2654435761u) ^ seed));

        int headWords = 1 + rnd.Next(2);
        var head = new StringBuilder();
        for (int w = 0; w < headWords; w++)
        {
            if (w > 0) head.Append(' ');
            head.Append(Vocabulary[rnd.Next(Vocabulary.Length)]);
        }

        int bodyWords = 20 + rnd.Next(20);
        var body = new StringBuilder(bodyWords * 8);
        for (int w = 0; w < bodyWords; w++)
        {
            if (w > 0) body.Append(' ');
            body.Append(Vocabulary[rnd.Next(Vocabulary.Length)]);
        }

        return new SearchDocument
        {
            Id = id,
            Headword = head.ToString(),
            Content = body.ToString(),
        };
    }

    /// <summary>Generates a batch of fresh documents starting at a high id (for insert benchmarks).</summary>
    public static List<SearchDocument> GenerateBatch(int count, long startId, int seed = 777)
    {
        var list = new List<SearchDocument>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add(Create(startId + i, seed));
        }
        return list;
    }
}
