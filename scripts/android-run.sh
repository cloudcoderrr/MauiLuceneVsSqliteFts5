#!/usr/bin/env bash
# Installs the APK on a booted emulator, launches the benchmark in headless mode,
# waits for completion via logcat, and pulls the result files.
#
# Usage: scripts/android-run.sh <apk> <output-dir>
# Env:   BENCH_DATASETS, BENCH_ENGINES, BENCH_MILLION_COUNT, COLLECTION_DB_LOCAL
set -euo pipefail

APK="$1"
OUT_LOCAL="${2:-android-results}"
PKG="com.companyname.mauibenchmark"
ACT="$PKG/com.companyname.mauibenchmark.MainActivity"
# Use the app's INTERNAL files dir. Files that adb pushes to the external
# /sdcard/Android/data/<pkg>/files are not visible to the app under scoped
# storage, so we stage via /data/local/tmp + run-as (the Debug APK is debuggable).
INTERNAL_DIR="/data/user/0/$PKG/files"

mkdir -p "$OUT_LOCAL"

echo "Waiting for device..."
adb wait-for-device
adb root || true
adb wait-for-device
adb shell 'while [[ -z $(getprop sys.boot_completed) ]]; do sleep 1; done'
adb shell input keyevent 82 || true

echo "Installing $APK"
adb install -r -g "$APK"

adb shell run-as "$PKG" mkdir -p files || true

EXTRAS=(--ez BENCH_AUTORUN true
        --es BENCH_RESULTS_DIR "$INTERNAL_DIR"
        --es BENCH_DATASETS "${BENCH_DATASETS:-1000,1000000}"
        --es BENCH_ENGINES "${BENCH_ENGINES:-lucene,fts5}")

if [ -n "${BENCH_MILLION_COUNT:-}" ]; then
  EXTRAS+=(--es BENCH_MILLION_COUNT "$BENCH_MILLION_COUNT")
fi

if [ -n "${COLLECTION_DB_LOCAL:-}" ] && [ -f "${COLLECTION_DB_LOCAL}" ]; then
  echo "Staging Collection.db ($(wc -c < "$COLLECTION_DB_LOCAL") bytes) into app internal storage..."
  adb push "$COLLECTION_DB_LOCAL" /data/local/tmp/Collection.db
  adb shell run-as "$PKG" cp /data/local/tmp/Collection.db files/Collection.db
  adb shell rm -f /data/local/tmp/Collection.db || true
  EXTRAS+=(--es BENCH_COLLECTION_DB "$INTERNAL_DIR/Collection.db")
fi

echo "Clearing previous result files and logcat..."
adb shell run-as "$PKG" rm -f files/bench-results.json files/bench-results.md || true
adb logcat -c || true

# Capture the full logcat for debugging and marker detection.
adb logcat -v time > "$OUT_LOCAL/logcat-full.txt" 2>&1 &
LOGPID=$!

echo "Launching: adb shell am start -n $ACT ${EXTRAS[*]}"
adb shell am start -n "$ACT" "${EXTRAS[@]}"

echo "Waiting for benchmark to finish..."
DEADLINE=$(( SECONDS + 10800 ))
START_GRACE=$(( SECONDS + 90 ))
STATUS="timeout"
while [ $SECONDS -lt $DEADLINE ]; do
  if grep -q "===BENCH_DONE===" "$OUT_LOCAL/logcat-full.txt" 2>/dev/null; then STATUS="done"; break; fi
  if grep -q "===BENCH_ERROR===" "$OUT_LOCAL/logcat-full.txt" 2>/dev/null; then STATUS="error"; sleep 5; break; fi
  # Fast-fail if the app process died without finishing (crash).
  if [ $SECONDS -gt $START_GRACE ]; then
    if [ -z "$(adb shell pidof "$PKG" 2>/dev/null | tr -d '\r')" ]; then
      echo "App process is no longer running; waiting briefly for final logs..."
      sleep 10
      if grep -q "===BENCH_DONE===" "$OUT_LOCAL/logcat-full.txt" 2>/dev/null; then STATUS="done"; else STATUS="crashed"; fi
      break
    fi
  fi
  sleep 10
done

kill "$LOGPID" >/dev/null 2>&1 || true

echo "Pulling results (status=$STATUS)..."
adb exec-out run-as "$PKG" cat files/bench-results.json > "$OUT_LOCAL/bench-results.json" 2>/dev/null || true
adb exec-out run-as "$PKG" cat files/bench-results.md > "$OUT_LOCAL/bench-results.md" 2>/dev/null || true
if [ ! -s "$OUT_LOCAL/bench-results.json" ]; then rm -f "$OUT_LOCAL/bench-results.json"; fi
if [ ! -s "$OUT_LOCAL/bench-results.md" ]; then rm -f "$OUT_LOCAL/bench-results.md"; fi

# Fallback: reconstruct results from logcat (the app also prints the JSON there).
if [ ! -f "$OUT_LOCAL/bench-results.json" ]; then
  echo "Results file not found on device; extracting from logcat..."
  grep 'FTSBENCH' "$OUT_LOCAL/logcat-full.txt" | sed -E 's/^.*FTSBENCH[^:]*: ?//' > "$OUT_LOCAL/ftsbench.txt"
  awk '/===BENCH_RESULTS_BEGIN===/{f=1;next} /===BENCH_RESULTS_END===/{f=0} f' "$OUT_LOCAL/ftsbench.txt" > "$OUT_LOCAL/bench-results.json"
  if [ -s "$OUT_LOCAL/bench-results.json" ]; then
    echo "Recovered results JSON from logcat ($(wc -c < "$OUT_LOCAL/bench-results.json") bytes)."
  else
    rm -f "$OUT_LOCAL/bench-results.json"
  fi
fi

echo "===== logcat tail (FTSBENCH) ====="
grep "FTSBENCH" "$OUT_LOCAL/logcat-full.txt" | tail -n 80 || true
echo "===== crash tail (if any) ====="
grep -E "AndroidRuntime|FATAL|mono-rt|monodroid" "$OUT_LOCAL/logcat-full.txt" | tail -n 40 || true

if [ ! -f "$OUT_LOCAL/bench-results.json" ]; then
  echo "No results produced (status=$STATUS)" >&2
  exit 1
fi
echo "Android run complete (status=$STATUS)."
