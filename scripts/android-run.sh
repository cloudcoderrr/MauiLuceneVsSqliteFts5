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
# Results go to the app's EXTERNAL files dir, which the app writes and adb can pull.
# The 813 MB Collection.db is staged in /data/local/tmp and read directly by the app:
# adb-pushed files are not visible inside the app's external dir, and the Release APK
# is not debuggable, so run-as is unavailable.
DEVICE_DIR="/sdcard/Android/data/$PKG/files"

mkdir -p "$OUT_LOCAL"

echo "Waiting for device..."
adb wait-for-device
adb root || true
adb wait-for-device
adb shell 'while [[ -z $(getprop sys.boot_completed) ]]; do sleep 1; done'
adb shell input keyevent 82 || true
# Permissive SELinux lets the app read the staged DB under /data/local/tmp.
adb shell setenforce 0 || true

echo "Installing $APK"
adb install -r -g "$APK"

adb shell mkdir -p "$DEVICE_DIR" || true

EXTRAS=(--ez BENCH_AUTORUN true
        --es BENCH_RESULTS_DIR "$DEVICE_DIR"
        --es BENCH_DATASETS "${BENCH_DATASETS:-1000,1000000}"
        --es BENCH_ENGINES "${BENCH_ENGINES:-lucene,fts5}")

if [ -n "${BENCH_MILLION_COUNT:-}" ]; then
  EXTRAS+=(--es BENCH_MILLION_COUNT "$BENCH_MILLION_COUNT")
fi

if [ -n "${COLLECTION_DB_LOCAL:-}" ] && [ -f "${COLLECTION_DB_LOCAL}" ]; then
  echo "Staging Collection.db ($(wc -c < "$COLLECTION_DB_LOCAL") bytes) into /data/local/tmp..."
  adb push "$COLLECTION_DB_LOCAL" /data/local/tmp/Collection.db
  adb shell chmod 644 /data/local/tmp/Collection.db || true
  EXTRAS+=(--es BENCH_COLLECTION_DB "/data/local/tmp/Collection.db")
fi

echo "Clearing previous result files and logcat..."
adb shell rm -f "$DEVICE_DIR/bench-results.json" "$DEVICE_DIR/bench-results.md" || true
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
adb pull "$DEVICE_DIR/bench-results.json" "$OUT_LOCAL/bench-results.json" 2>/dev/null || true
adb pull "$DEVICE_DIR/bench-results.md" "$OUT_LOCAL/bench-results.md" 2>/dev/null || true
[ -s "$OUT_LOCAL/bench-results.json" ] || rm -f "$OUT_LOCAL/bench-results.json"
[ -s "$OUT_LOCAL/bench-results.md" ] || rm -f "$OUT_LOCAL/bench-results.md"

# Fallback: reconstruct results from logcat (the app prints JSON and markdown there).
grep 'FTSBENCH' "$OUT_LOCAL/logcat-full.txt" | sed -E 's/^.*FTSBENCH[^:]*: ?//' > "$OUT_LOCAL/ftsbench.txt" || true
if [ ! -f "$OUT_LOCAL/bench-results.json" ]; then
  awk '/===BENCH_RESULTS_BEGIN===/{f=1;next} /===BENCH_RESULTS_END===/{f=0} f' "$OUT_LOCAL/ftsbench.txt" > "$OUT_LOCAL/bench-results.json"
  if [ -s "$OUT_LOCAL/bench-results.json" ]; then echo "Recovered results JSON from logcat."; else rm -f "$OUT_LOCAL/bench-results.json"; fi
fi
if [ ! -f "$OUT_LOCAL/bench-results.md" ]; then
  awk '/===BENCH_MD_BEGIN===/{f=1;next} /===BENCH_MD_END===/{f=0} f' "$OUT_LOCAL/ftsbench.txt" > "$OUT_LOCAL/bench-results.md"
  if [ -s "$OUT_LOCAL/bench-results.md" ]; then echo "Recovered results MD from logcat."; else rm -f "$OUT_LOCAL/bench-results.md"; fi
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
