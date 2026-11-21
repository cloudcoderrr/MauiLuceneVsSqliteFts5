# MauiLuceneVsSqliteFts5

A .NET MAUI benchmark app comparing **Lucene.NET** and **SQLite FTS5** full-text search on
**Android, Windows and iOS**.

## What it measures

For each engine (Lucene.NET, SQLite FTS5) and each dataset (1,000 synthetic records,
1,000,000 synthetic records, and the real `Collection.db` dictionary table):

1. **Create index** time
2. **Simple query** time (full-text search for a common term; average of several runs)
3. **Simple insert** time (inserting new records into the already-built index)
4. **Storage usage** of the built index

### Engine configuration

- **Lucene.NET** uses `StandardAnalyzer`. Index creation uses **bulk-import tuning**
  (a 64 MB RAM buffer and no compound file) so large sets build with fewer, larger segment
  flushes.
- **SQLite FTS5** uses the `unicode61` tokenizer. `Collection.db` is WAL-mode and is opened
  read-only with `immutable=1` (so it works even from a non-writable directory).
- CI builds are **Release**, with **AOT** enabled on Android (`RunAOTCompilation`) and iOS.

## Projects

| Project | Purpose |
|---|---|
| `src/Benchmarks.Core` | Engine abstraction, Lucene.NET + FTS5 implementations, data sources, runner |
| `src/MauiBenchmark` | MAUI app (Android/iOS/Windows): interactive UI + headless CI auto-run |
| `src/Benchmarks.ConsoleRunner` | Desktop console host (used for local runs and the Windows CI job) |

## Data

- **Synthetic datasets** are generated deterministically (reproducible across platforms).
- **`Collection.db`** is a ~813 MB SQLite database. Its `Notes_*` tables use a named
  `collation_en_us` collation; the app registers an English (case/diacritic-insensitive)
  collation via `SqliteConnection.CreateCollation`. The benchmark indexes the largest note
  table's `C1` (headword) + `C2` (HTML entry) columns (~322k rows).
- Because `Collection.db` exceeds GitHub's 100 MB file limit, it is split into < 95 MB parts
  (`scripts/split-db.ps1`) distributed via a GitHub Release and reassembled at test time
  (`scripts/join-db.sh` / `join-db.ps1`).

## Running locally

Console host (Windows/macOS/Linux):

```pwsh
$env:BENCH_DATASETS = "1000,1000000,collection"
$env:BENCH_ENGINES  = "lucene,fts5"
$env:BENCH_COLLECTION_DB = "$PWD/Test/Collection.db"
$env:BENCH_RESULTS_DIR   = "$PWD/results"
dotnet run --project src/Benchmarks.ConsoleRunner -c Release
```

MAUI app: build/deploy `src/MauiBenchmark` to a device, pick datasets/engines, tap **Run**.

## CI

`.github/workflows/benchmark.yml` runs the benchmarks on GitHub VMs (all **Release**):

- **Windows** — console host on `windows-latest` (Release, JIT)
- **Android** — MAUI APK (Release + AOT) on an x86_64 emulator (`reactivecircus/android-emulator-runner`)
- **iOS** — MAUI app (Release + AOT) on an iOS 26 simulator (`xcrun simctl`) on `macos-26`

Trigger with:

```sh
gh workflow run "FTS Benchmark" -f datasets=1000,1000000,collection -f engines=lucene,fts5
# optional: -f only_android=true  or  -f only_ios=true
```

Results (`bench-results.json` / `bench-results.md`) and the Android APK are uploaded as
artifacts and summarized in the workflow run summary.

## Results (Release / AOT)

Measured on GitHub-hosted runners with .NET 10. Hit counts are identical on every platform
(223 / 230 / 231,133 / 241,244 / 278,524), confirming correctness and the shared
`collation_en_us` English collation. `Create` = index build time, `Query` = average of 5 runs
of a common-term search, `Insert` = 1,000 new rows into the already-built index.

### Windows — `windows-latest`, Release (JIT), x64, 4 CPU

| Engine | Dataset | Create index | Query (avg) | Insert 1k | Storage |
|---|---|--:|--:|--:|--:|
| Lucene.NET | 1,000 | 1,676 ms | 0.17 ms | 135 ms | 70 KB |
| SQLite FTS5 | 1,000 | 1,156 ms | 0.42 ms | 76 ms | 436 KB |
| Lucene.NET | 1,000,000 | 16,958 ms | 14.9 ms | 55 ms | 60.7 MB |
| SQLite FTS5 | 1,000,000 | 17,279 ms | 378 ms | 178 ms | 394 MB |
| Lucene.NET | Collection.db (322,943) | 33,908 ms | 35.6 ms | 59 ms | 169 MB |
| SQLite FTS5 | Collection.db (322,943) | 28,540 ms | 412 ms | 140 ms | 1.05 GB |

### Android — emulator API 34 (Android 14), Release + AOT, x86_64, 2 CPU

| Engine | Dataset | Create index | Query (avg) | Insert 1k | Storage |
|---|---|--:|--:|--:|--:|
| Lucene.NET | 1,000 | 1,044 ms | 0.06 ms | 90 ms | 70 KB |
| SQLite FTS5 | 1,000 | 252 ms | 0.33 ms | 9 ms | 436 KB |
| Lucene.NET | 1,000,000 | 43,075 ms | 10.8 ms | 57 ms | 60.7 MB |
| SQLite FTS5 | 1,000,000 | 19,516 ms | 242 ms | 17 ms | 394 MB |
| Lucene.NET | Collection.db | 59,508 ms | 6.5 ms | 31 ms | 169 MB |
| SQLite FTS5 | Collection.db | 22,844 ms | 234 ms | 21 ms | 1.05 GB |

### iOS — simulator iOS 26.5, Release + AOT, arm64, 3 CPU

| Engine | Dataset | Create index | Query (avg) | Insert 1k | Storage |
|---|---|--:|--:|--:|--:|
| Lucene.NET | 1,000 | 292 ms | 0.04 ms | 47 ms | 70 KB |
| SQLite FTS5 | 1,000 | 43 ms | 0.26 ms | 10 ms | 436 KB |
| Lucene.NET | 1,000,000 | 54,304 ms | 7.8 ms | 52 ms | 60.7 MB |
| SQLite FTS5 | 1,000,000 | 20,973 ms | 433 ms | 47 ms | 394 MB |
| Lucene.NET | Collection.db | 103,864 ms | 18.1 ms | 70 ms | 169 MB |
| SQLite FTS5 | Collection.db | 45,187 ms | 421 ms | 51 ms | 1.05 GB |

### Takeaways

- **Index build:** SQLite FTS5 builds faster than Lucene on every platform and dataset
  (native SQLite). Lucene with bulk-import tuning is competitive on desktop but 2–3× slower
  on the mobile emulators/simulators.
- **Storage:** Lucene is far more compact (Collection.db: **169 MB vs 1.05 GB**; 1M:
  61 MB vs 394 MB) because the FTS5 table stores the full content alongside the index.
- **Query (high-frequency term):** Lucene is dramatically faster (6–36 ms) for a term that
  matches hundreds of thousands of rows; FTS5 `ORDER BY rank` over that many matches is the
  slow path (234–433 ms). For *selective* queries FTS5 is also fast.
- **Insert:** Both are fast (tens of ms for 1,000 rows); FTS5 is generally quicker.
- **AOT matters:** enabling AOT cut Lucene's mobile index-build time massively vs the Debug
  interpreter/JIT (e.g. iOS Collection.db **1,054 s → 104 s**, Android 1M **239 s → 43 s**).