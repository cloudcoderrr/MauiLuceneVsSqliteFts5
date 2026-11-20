using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace Benchmarks.Core;

/// <summary>Lucene.NET 4.8 full-text engine using an on-disk FSDirectory index.</summary>
public sealed class LuceneSearchEngine : ISearchEngine
{
    private const LuceneVersion Version = LuceneVersion.LUCENE_48;
    private const string IdField = "id";
    private const string HeadwordField = "headword";
    private const string ContentField = "content";

    // RAM buffer used for bulk index creation. Kept moderate so it is safe on mobile heaps.
    private const double BulkRamBufferMB = 64;

    private readonly string _indexDir;
    private DirectoryReader? _reader;
    private IndexSearcher? _searcher;
    private FSDirectory? _searchDirectory;

    public LuceneSearchEngine(string workDir)
    {
        _indexDir = workDir;
        System.IO.Directory.CreateDirectory(_indexDir);
    }

    public string Name => "Lucene.NET";

    // Each index directory is only ever written by a single writer at a time in this benchmark,
    // so we disable file locking. Native FS locks are unreliable on some mobile filesystems
    // and can cause "Lock obtain timed out" errors.
    private static FSDirectory OpenDir(string path)
    {
        var dir = FSDirectory.Open(new DirectoryInfo(path));
        dir.SetLockFactory(NoLockFactory.GetNoLockFactory());
        return dir;
    }

    public long CreateIndex(IDocumentSource source, Action<long>? progress = null)
    {
        using var dir = OpenDir(_indexDir);
        using var analyzer = new StandardAnalyzer(Version);
        var config = new IndexWriterConfig(Version, analyzer)
        {
            OpenMode = OpenMode.CREATE,
            // Bulk-import tuning: a large RAM buffer flushes far fewer, larger segments,
            // and skipping the compound-file format avoids packing work during the build.
            RAMBufferSizeMB = BulkRamBufferMB,
            UseCompoundFile = false,
        };
        using var writer = new IndexWriter(dir, config);

        long count = 0;
        foreach (var d in source.GetDocuments())
        {
            writer.AddDocument(BuildDocument(d));
            count++;
            if (progress is not null && (count % 20000) == 0)
            {
                progress(count);
            }
        }

        writer.Commit();
        return count;
    }

    public long GetIndexSizeBytes() => DirectorySize(_indexDir);

    public void PrepareQuery()
    {
        _searchDirectory = OpenDir(_indexDir);
        _reader = DirectoryReader.Open(_searchDirectory);
        _searcher = new IndexSearcher(_reader);
    }

    public long CountHits(string term)
    {
        var searcher = _searcher ?? throw new InvalidOperationException("Call PrepareQuery first.");
        var query = new TermQuery(new Term(ContentField, term.ToLowerInvariant()));
        return searcher.Search(query, 1).TotalHits;
    }

    public int RunQuery(string term, int topN)
    {
        var searcher = _searcher ?? throw new InvalidOperationException("Call PrepareQuery first.");
        var query = new TermQuery(new Term(ContentField, term.ToLowerInvariant()));
        var hits = searcher.Search(query, topN);
        return hits.ScoreDocs.Length;
    }

    public void Insert(IReadOnlyList<SearchDocument> documents)
    {
        using var dir = OpenDir(_indexDir);
        using var analyzer = new StandardAnalyzer(Version);
        var config = new IndexWriterConfig(Version, analyzer)
        {
            OpenMode = OpenMode.APPEND,
        };
        using var writer = new IndexWriter(dir, config);
        foreach (var d in documents)
        {
            writer.AddDocument(BuildDocument(d));
        }
        writer.Commit();
    }

    private static Document BuildDocument(SearchDocument d)
    {
        return new Document
        {
            new StringField(IdField, d.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), Field.Store.YES),
            new TextField(HeadwordField, d.Headword ?? string.Empty, Field.Store.NO),
            new TextField(ContentField, d.Content ?? string.Empty, Field.Store.NO),
        };
    }

    private static long DirectorySize(string path)
    {
        if (!System.IO.Directory.Exists(path))
        {
            return 0;
        }

        long total = 0;
        foreach (var file in System.IO.Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (FileNotFoundException)
            {
                // File may be a transient Lucene lock/segment; ignore.
            }
        }
        return total;
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _searchDirectory?.Dispose();
        _reader = null;
        _searcher = null;
        _searchDirectory = null;
    }
}
