#!/bin/bash
# Serialized dotnet test. Usage: scripts/test.sh <project> [dotnet test args...]
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
source scripts/dev-env.sh
if [ $# -lt 1 ]; then echo "usage: scripts/test.sh <project> [dotnet test args...]" >&2; exit 2; fi
TARGET="$1"; shift
LOCK="$ROOT/.msbuild-lock.d"
tries=0
while ! mkdir "$LOCK" 2>/dev/null; do
  tries=$((tries+1))
  if [ $((tries % 120)) -eq 0 ]; then echo "waiting for build lock ($LOCK)..." >&2; fi
  sleep 0.5
done
trap 'rmdir "$LOCK" 2>/dev/null || true' EXIT
dotnet test "$TARGET" "$@"
