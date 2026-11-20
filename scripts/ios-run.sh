#!/usr/bin/env bash
# Boots an iOS simulator, installs the app, runs the benchmark headless, and copies results out.
#
# Usage: scripts/ios-run.sh <app-path> <output-dir>
# Env:   BENCH_DATASETS, BENCH_ENGINES, BENCH_MILLION_COUNT, COLLECTION_DB_LOCAL
set -euo pipefail

APP="$1"
OUT_LOCAL="${2:-ios-results}"
BUNDLE_ID="com.companyname.mauibenchmark"

mkdir -p "$OUT_LOCAL"

echo "Available simulator runtimes:"
xcrun simctl list runtimes ios

# Pick the newest available iOS runtime identifier.
RUNTIME=$(xcrun simctl list runtimes ios -j | python3 -c "import sys,json; r=[x for x in json.load(sys.stdin)['runtimes'] if x.get('isAvailable')]; print(sorted(r, key=lambda x: x['version'])[-1]['identifier'])")
echo "Using runtime: $RUNTIME"

# Choose an iPhone device type that the runtime supports.
DEVTYPE=$(xcrun simctl list devicetypes -j | python3 -c "import sys,json; d=[x for x in json.load(sys.stdin)['devicetypes'] if 'iPhone' in x['name']]; print(sorted(d, key=lambda x: x['name'])[-1]['identifier'])")
echo "Using device type: $DEVTYPE"

UDID=$(xcrun simctl create "bench-iphone" "$DEVTYPE" "$RUNTIME")
echo "Created simulator $UDID"

cleanup() {
  xcrun simctl terminate "$UDID" "$BUNDLE_ID" >/dev/null 2>&1 || true
  xcrun simctl shutdown "$UDID" >/dev/null 2>&1 || true
  xcrun simctl delete "$UDID" >/dev/null 2>&1 || true
}
trap cleanup EXIT

xcrun simctl boot "$UDID"
xcrun simctl bootstatus "$UDID" -b

echo "Installing $APP"
xcrun simctl install "$UDID" "$APP"

CONTAINER=$(xcrun simctl get_app_container "$UDID" "$BUNDLE_ID" data)
DOCS="$CONTAINER/Documents"
mkdir -p "$DOCS"
echo "App data container: $CONTAINER"

export SIMCTL_CHILD_BENCH_AUTORUN=1
export SIMCTL_CHILD_BENCH_RESULTS_DIR="$DOCS"
export SIMCTL_CHILD_BENCH_DATASETS="${BENCH_DATASETS:-1000,1000000}"
export SIMCTL_CHILD_BENCH_ENGINES="${BENCH_ENGINES:-lucene,fts5}"
if [ -n "${BENCH_MILLION_COUNT:-}" ]; then
  export SIMCTL_CHILD_BENCH_MILLION_COUNT="$BENCH_MILLION_COUNT"
fi
if [ -n "${COLLECTION_DB_LOCAL:-}" ] && [ -f "${COLLECTION_DB_LOCAL}" ]; then
  echo "Copying Collection.db into simulator container..."
  cp "$COLLECTION_DB_LOCAL" "$DOCS/Collection.db"
  export SIMCTL_CHILD_BENCH_COLLECTION_DB="$DOCS/Collection.db"
fi

rm -f "$DOCS/bench-results.json" "$DOCS/bench-results.md" || true

echo "Launching app (console attached)..."
( xcrun simctl launch --console "$UDID" "$BUNDLE_ID" > "$OUT_LOCAL/console.log" 2>&1 ) &
LPID=$!

DEADLINE=$(( SECONDS + 10800 ))
STATUS="timeout"
while kill -0 "$LPID" 2>/dev/null; do
  if [ -f "$DOCS/bench-results.json" ] && grep -q "===BENCH_DONE===" "$OUT_LOCAL/console.log" 2>/dev/null; then
    STATUS="done"; break
  fi
  if [ $SECONDS -ge $DEADLINE ]; then
    echo "Timeout waiting for benchmark"; break
  fi
  sleep 10
done

# Give the app a moment to flush, then stop it.
sleep 3
xcrun simctl terminate "$UDID" "$BUNDLE_ID" >/dev/null 2>&1 || true
kill "$LPID" >/dev/null 2>&1 || true

echo "Copying results (status=$STATUS)..."
cp "$DOCS/bench-results.json" "$OUT_LOCAL/bench-results.json" 2>/dev/null || true
cp "$DOCS/bench-results.md" "$OUT_LOCAL/bench-results.md" 2>/dev/null || true

echo "===== console tail ====="
tail -n 60 "$OUT_LOCAL/console.log" || true

if [ ! -f "$OUT_LOCAL/bench-results.json" ]; then
  echo "No results produced (status=$STATUS)" >&2
  exit 1
fi
echo "iOS run complete (status=$STATUS)."
