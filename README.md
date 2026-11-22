# MauiLuceneVsSqliteFts5

A .NET MAUI benchmark app comparing **Lucene.NET** and **SQLite FTS5** full-text search on
**Android, Windows and iOS**.

## What it measures

For each engine (Lucene.NET, SQLite FTS5, SQLite plain index) and each dataset (1,000 synthetic
records, 1,000,000 synthetic records, and the real `Collection.db` dictionary table):

1. **Create index** time
2. **Simple query** time (full-text search for a common term; average of several runs)
3. **Simple insert** time (inserting new records into the already-built index)
4. **Storage usage** of the built index

### Engines and queries

Three engines are benchmarked:

- **Lucene.NET** (`StandardAnalyzer`). Index creation uses **bulk-import tuning** (a 64 MB RAM
  buffer and no compound file). Query types measured: *Simple* (term), *Fuzzy* (1 and 2 edits),
  *Wildcard*, *Regexp*, and *Leading wildcard*.
- **SQLite FTS5** (`unicode61` tokenizer) — full-text `MATCH`.
- **SQLite Index** — a plain table with a B-tree index on the headword, queried by **exact
  match** (`= value`, no `LIKE`).

`Collection.db` is WAL-mode and is opened read-only with `immutable=1` (so it works even from a
non-writable directory). CI builds are **Release**, with **AOT** on Android (`RunAOTCompilation`)
and iOS.

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

Measured on GitHub-hosted runners with .NET 10, comparing **Lucene.NET**, **SQLite FTS5**, and a
plain **SQLite Index** (B-tree on the headword, exact match). Hit counts are identical across
platforms, confirming correctness and the shared `collation_en_us` English collation. Windows is
Release/JIT; Android and iOS are Release + **AOT**.

### Index build time (ms) & storage

| Engine | Dataset | Windows | Android | iOS | Storage |
|---|---|--:|--:|--:|--:|
| Lucene.NET | 1,000 | 743 | 614 | 413 | 70 KB |
| SQLite FTS5 | 1,000 | 318 | 75 | 51 | 436 KB |
| SQLite Index | 1,000 | 68 | 24 | 10 | 288 KB |
| Lucene.NET | 1,000,000 | 14,330 | 76,922 | 39,174 | 60.7 MB |
| SQLite FTS5 | 1,000,000 | 11,511 | 20,039 | 46,294 | 394 MB |
| SQLite Index | 1,000,000 | 5,195 | 11,253 | 5,351 | 267 MB |
| Lucene.NET | Collection.db | 25,607 | 91,193 | 54,561 | 169 MB |
| SQLite FTS5 | Collection.db | 21,294 | 28,261 | 27,453 | 1.05 GB |
| SQLite Index | Collection.db | 8,761 | 12,047 | 9,486 | 808 MB |

### Query latency — Collection.db (322,943 rows), ms (avg of 5)

| Engine | Query | Windows | Android | iOS | Hits |
|---|---|--:|--:|--:|--:|
| Lucene.NET | Simple (term) | 29.8 | 18.5 | 5.5 | 278,524 |
| Lucene.NET | Fuzzy (1 edit) | 41.3 | 23.0 | 11.3 | 278,527 |
| Lucene.NET | Fuzzy (2 edits) | 27.2 | 55.0 | 29.4 | 278,530 |
| Lucene.NET | Wildcard (`cont*`) | 6.0 | 15.3 | 6.9 | 278,608 |
| Lucene.NET | Regexp (`cont.*`) | 6.0 | 15.2 | 7.0 | 278,608 |
| Lucene.NET | Leading wildcard (`*iner`) | 51.4 | 217.1 | 112.9 | 278,531 |
| SQLite FTS5 | Simple (`MATCH`) | 308.5 | 340.2 | 259.4 | 278,524 |
| SQLite Index | Exact match (`= value`) | 0.03 | 0.03 | 0.03 | 1 |

Synthetic 1k/1M query numbers and the full per-platform tables are in the run artifacts.

### Takeaways

- **Build speed:** the plain **SQLite Index** builds fastest everywhere (Collection.db 8.8–12 s),
  then FTS5, then Lucene; on mobile the AOT Lucene build is slowest.
- **Storage:** Lucene is most compact; the plain index (content + B-tree) is mid-sized; FTS5 is
  largest because it stores content plus the full inverted index (Collection.db: 169 MB vs
  808 MB vs 1.05 GB).
- **Exact match (SQLite Index):** sub-0.1 ms regardless of size — an indexed equality lookup is
  by far the cheapest query, but only answers exact-key lookups.
- **Lucene query types:** Simple/Wildcard/Regexp (prefix-anchored) are fastest; **Fuzzy(2)** and
  **Leading wildcard** are slowest because they enumerate many terms (Leading wildcard on
  Collection.db: 51–217 ms).
- **FTS5 full-text:** a high-frequency `MATCH` ranked over ~278k hits is the slow path
  (259–340 ms); for selective terms it is fast.
- **AOT matters:** AOT cut Lucene's mobile build time massively vs the Debug interpreter/JIT
  (e.g. iOS Collection.db build **1,054 s → 55 s**).