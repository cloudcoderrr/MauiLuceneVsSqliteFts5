# MauiLuceneVsSqliteFts5

A .NET MAUI benchmark app comparing **Lucene.NET**, **SQLite FTS5**, and **SQLite B-tree
lookups** on **Android, Windows and iOS**.

## What it measures

For each engine (Lucene.NET, SQLite FTS5, SQLite plain index) and each dataset (1,000 synthetic
records, 1,000,000 synthetic records, and the real `Collection.db` dictionary table):

1. **Index build time** and **storage** after the build.
2. **Mean query latency** and **hit count** for each supported query.
3. **Total and per-record time** to insert 1,000 records into the built index.

By default, each query runs in **15 untimed warmup rounds** and **30 timed rounds**.
Each round runs every query for the current engine and dataset once, rotating the starting
query. The benchmark times searches for up to 50 results and counts total hits afterward.
Query settings are included in the JSON and Markdown reports.

### Engines and queries

Three engines are benchmarked:

- **Lucene.NET** (`StandardAnalyzer`). Index creation uses **bulk-import tuning** (a 64 MB RAM
  buffer and no compound file). Query types measured: *Simple* (term), *Fuzzy* (1 and 2 edits),
  *Wildcard*, *Regexp*, and *Leading wildcard*. The term query ranks by relevance. Wildcard
  and regexp queries use Lucene's default constant-score rewrite.
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
$env:BENCH_ENGINES  = "lucene,fts5,index"
$env:BENCH_COLLECTION_DB = "$PWD/Test/Collection.db"
$env:BENCH_RESULTS_DIR   = "$PWD/results"
dotnet run --project src/Benchmarks.ConsoleRunner -c Release
```

Set `BENCH_QUERY_WARMUP` to change the warmup rounds (0 disables them),
`BENCH_QUERY_REPEAT` to change the timed rounds, or `BENCH_QUERY_TOPN` to change the
result limit.

MAUI app: build/deploy `src/MauiBenchmark` to a device, pick datasets/engines, tap **Run**.

## CI

`.github/workflows/benchmark.yml` runs the benchmarks on GitHub VMs (all **Release**):

- **Windows** — console host on `windows-latest` (Release, JIT)
- **Android** — MAUI APK (Release + AOT) on an x86_64 emulator (`reactivecircus/android-emulator-runner`)
- **iOS** — MAUI app (Release + AOT) on an iOS 26 simulator (`xcrun simctl`) on `macos-26`

Trigger with:

```sh
gh workflow run "FTS Benchmark" -f datasets=1000,1000000,collection -f engines=lucene,fts5,index
# optional: -f only_android=true  or  -f only_ios=true
```

Results (`bench-results.json` / `bench-results.md`) and the Android APK are uploaded as
artifacts and summarized in the workflow run summary.

## Results with query warmup

The Windows, Android, and iOS CI results are from [this benchmark run](https://github.com/cloudcoderrr/MauiLuceneVsSqliteFts5/actions/runs/44997677151).
The local Windows run independently used the same datasets and query settings.
Every run requested up to 50 results, with 15 warmup and 30 timed rounds per query.

| Runner | OS | Architecture | Logical CPUs | Runtime | Build | Run time (s) |
|---|---|---|---|---|---|--:|
| Windows CI | Microsoft Windows 10.0.26100 | X64 | 4 | .NET 10.0.15 | Release / JIT | 121 |
| Android CI | 14 | X64 | 2 | .NET 10.0.15 | Release / AOT | 199 |
| iOS CI | 26.5 | Arm64 | 3 | .NET 10.0.15 | Release / AOT | 185 |
| Windows local | Microsoft Windows 10.0.19045 | X64 | 4 | .NET 10.0.14 | Release / JIT | 230 |

### Index build time (ms)

| Engine | Dataset | Windows CI | Android CI | iOS CI | Windows local |
|---|---|--:|--:|--:|--:|
| Lucene.NET | 1,000 synthetic | 701 | 757 | 290 | 1,295 |
| SQLite FTS5 | 1,000 synthetic | 229 | 88 | 28 | 107 |
| SQLite Index | 1,000 synthetic | 54 | 36 | 9 | 35 |
| Lucene.NET | 1,000,000 synthetic | 14,061 | 40,460 | 33,199 | 22,257 |
| SQLite FTS5 | 1,000,000 synthetic | 11,556 | 15,984 | 13,475 | 30,435 |
| SQLite Index | 1,000,000 synthetic | 5,144 | 7,835 | 5,746 | 20,116 |
| Lucene.NET | Collection.db | 25,479 | 56,905 | 54,002 | 46,010 |
| SQLite FTS5 | Collection.db | 21,650 | 24,359 | 22,314 | 40,543 |
| SQLite Index | Collection.db | 8,391 | 8,433 | 10,543 | 24,440 |

### Insert time for 1,000 records (total ms / ms per record)

| Engine | Dataset | Windows CI | Android CI | iOS CI | Windows local |
|---|---|--:|--:|--:|--:|
| Lucene.NET | 1,000 synthetic | 94 / 0.094 | 61 / 0.061 | 45 / 0.045 | 165 / 0.165 |
| SQLite FTS5 | 1,000 synthetic | 44 / 0.044 | 26 / 0.026 | 8 / 0.008 | 31 / 0.031 |
| SQLite Index | 1,000 synthetic | 32 / 0.032 | 22 / 0.022 | 5 / 0.005 | 24 / 0.024 |
| Lucene.NET | 1,000,000 synthetic | 52 / 0.052 | 65 / 0.065 | 35 / 0.035 | 57 / 0.057 |
| SQLite FTS5 | 1,000,000 synthetic | 111 / 0.111 | 18 / 0.018 | 31 / 0.031 | 64 / 0.064 |
| SQLite Index | 1,000,000 synthetic | 933 / 0.933 | 37 / 0.037 | 79 / 0.079 | 189 / 0.189 |
| Lucene.NET | Collection.db | 40 / 0.040 | 47 / 0.047 | 50 / 0.050 | 66 / 0.066 |
| SQLite FTS5 | Collection.db | 96 / 0.096 | 22 / 0.022 | 28 / 0.028 | 57 / 0.057 |
| SQLite Index | Collection.db | 173 / 0.173 | 13 / 0.013 | 14 / 0.014 | 68 / 0.068 |

### Index storage after build

| Engine | Dataset | Windows CI | Android CI | iOS CI | Windows local |
|---|---|--:|--:|--:|--:|
| Lucene.NET | 1,000 synthetic | 70.11 KB | 70.11 KB | 70.1 KB | 70.11 KB |
| SQLite FTS5 | 1,000 synthetic | 436 KB | 436 KB | 436 KB | 436 KB |
| SQLite Index | 1,000 synthetic | 288 KB | 288 KB | 288 KB | 288 KB |
| Lucene.NET | 1,000,000 synthetic | 60.68 MB | 60.68 MB | 60.68 MB | 60.68 MB |
| SQLite FTS5 | 1,000,000 synthetic | 394.11 MB | 394.11 MB | 394.11 MB | 394.11 MB |
| SQLite Index | 1,000,000 synthetic | 266.66 MB | 266.66 MB | 266.66 MB | 266.66 MB |
| Lucene.NET | Collection.db | 169.16 MB | 169.16 MB | 169.16 MB | 169.16 MB |
| SQLite FTS5 | Collection.db | 1.05 GB | 1.05 GB | 1.05 GB | 1.05 GB |
| SQLite Index | Collection.db | 807.78 MB | 807.78 MB | 807.78 MB | 807.78 MB |

### Mean query latency (ms) and total hits

The synthetic query term is `system`, with patterns `syst*`, `syst.*`, and `*stem`.
The `Collection.db` query term is `container`, with patterns `cont*`, `cont.*`, and `*iner`.
Hit counts agreed across all four runs.

#### 1,000 synthetic records

| Engine | Query | Windows CI | Android CI | iOS CI | Windows local | Hits |
|---|---|--:|--:|--:|--:|--:|
| Lucene.NET | Simple | 0.16 | 0.10 | 0.07 | 0.16 | 223 |
| Lucene.NET | Fuzzy(1 edit) | 0.56 | 0.74 | 0.64 | 0.64 | 223 |
| Lucene.NET | Fuzzy(2 edits) | 1.68 | 2.59 | 2.64 | 1.84 | 223 |
| Lucene.NET | Wildcard | 0.16 | 0.41 | 0.07 | 0.19 | 223 |
| Lucene.NET | Regexp | 0.15 | 0.96 | 0.06 | 0.19 | 223 |
| Lucene.NET | Leading wildcard | 0.16 | 0.36 | 0.05 | 0.16 | 223 |
| SQLite FTS5 | Simple | 0.28 | 0.72 | 0.22 | 0.47 | 230 |
| SQLite Index | Exact match | 0.03 | 0.03 | 0.03 | 0.11 | 1 |

#### 1,000,000 synthetic records

| Engine | Query | Windows CI | Android CI | iOS CI | Windows local | Hits |
|---|---|--:|--:|--:|--:|--:|
| Lucene.NET | Simple | 2.80 | 10.33 | 6.30 | 2.91 | 231,133 |
| Lucene.NET | Fuzzy(1 edit) | 3.10 | 12.36 | 6.11 | 3.26 | 231,133 |
| Lucene.NET | Fuzzy(2 edits) | 3.77 | 14.18 | 7.98 | 4.39 | 231,133 |
| Lucene.NET | Wildcard | 2.64 | 11.04 | 4.61 | 3.13 | 231,133 |
| Lucene.NET | Regexp | 2.56 | 8.00 | 6.23 | 3.03 | 231,133 |
| Lucene.NET | Leading wildcard | 2.68 | 8.50 | 5.44 | 2.74 | 231,133 |
| SQLite FTS5 | Simple | 277.49 | 235.20 | 227.85 | 411.65 | 241,244 |
| SQLite Index | Exact match | 0.03 | 0.03 | 0.05 | 0.25 | 4,467 |

#### Collection.db (322,943 records)

| Engine | Query | Windows CI | Android CI | iOS CI | Windows local | Hits |
|---|---|--:|--:|--:|--:|--:|
| Lucene.NET | Simple | 3.15 | 7.13 | 6.05 | 3.82 | 278,524 |
| Lucene.NET | Fuzzy(1 edit) | 5.98 | 17.83 | 12.80 | 6.73 | 278,527 |
| Lucene.NET | Fuzzy(2 edits) | 11.36 | 54.57 | 40.56 | 14.46 | 278,530 |
| Lucene.NET | Wildcard | 5.13 | 11.09 | 9.75 | 5.73 | 278,608 |
| Lucene.NET | Regexp | 5.11 | 11.57 | 11.62 | 5.92 | 278,608 |
| Lucene.NET | Leading wildcard | 36.68 | 246.21 | 155.64 | 46.13 | 278,531 |
| SQLite FTS5 | Simple | 312.96 | 229.48 | 272.09 | 405.10 | 278,524 |
| SQLite Index | Exact match | 0.03 | 0.02 | 0.05 | 0.09 | 1 |

### Observations

- On `Collection.db`, Lucene's `container` term query was the fastest Lucene query in all four
  runs. It averaged 3.15–7.13 ms, compared with 5.13–11.09 ms for `cont*` and 5.11–11.62 ms
  for `cont.*`.
- The leading wildcard `*iner` averaged 36.68–246.21 ms on `Collection.db`. It has to examine
  a broader range of the term dictionary than the prefix patterns.

The [earlier CI run](https://github.com/cloudcoderrr/MauiLuceneVsSqliteFts5/actions/runs/44987428130) used one warmup and five timed searches per query. See the [previous benchmark tables](https://github.com/cloudcoderrr/MauiLuceneVsSqliteFts5/blob/79e31a41b328104f6f3395430cbcb8023eb5d2c7/README.md#results-release--aot).
