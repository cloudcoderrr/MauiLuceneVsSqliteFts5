#!/usr/bin/env bash
# Reassembles Collection.db from the split parts and verifies the SHA256 checksum.
# Usage: scripts/join-db.sh [parts-dir] [output-file]
set -euo pipefail

PARTS_DIR="${1:-db-parts}"
OUT="${2:-Collection.db}"

if ! ls "$PARTS_DIR"/Collection.db.[0-9][0-9][0-9] >/dev/null 2>&1; then
  echo "No parts found in $PARTS_DIR (expected Collection.db.000, .001, ...)" >&2
  exit 1
fi

# Zero-padded numeric suffixes sort correctly with a plain glob.
cat "$PARTS_DIR"/Collection.db.[0-9][0-9][0-9] > "$OUT"

if [ -f "$PARTS_DIR/Collection.db.sha256" ]; then
  expected="$(cut -d' ' -f1 < "$PARTS_DIR/Collection.db.sha256")"
  if command -v sha256sum >/dev/null 2>&1; then
    actual="$(sha256sum "$OUT" | cut -d' ' -f1)"
  else
    actual="$(shasum -a 256 "$OUT" | cut -d' ' -f1)"
  fi
  if [ "$expected" != "$actual" ]; then
    echo "Checksum mismatch: expected $expected, got $actual" >&2
    exit 1
  fi
  echo "Checksum OK ($actual)"
fi

bytes=$(wc -c < "$OUT")
echo "Joined -> $OUT ($bytes bytes)"
