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

`.github/workflows/benchmark.yml` runs the benchmarks on GitHub VMs:

- **Windows** — console host on `windows-latest`
- **Android** — MAUI APK on an x86_64 emulator (`reactivecircus/android-emulator-runner`)
- **iOS** — MAUI app on an iOS simulator (`xcrun simctl`) on `macos-14`

Trigger with:

```sh
gh workflow run "FTS Benchmark" -f datasets=1000,1000000 -f engines=lucene,fts5
```

Results (`bench-results.json` / `bench-results.md`) are uploaded as artifacts and summarized
in the workflow run summary.