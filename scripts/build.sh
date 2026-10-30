#!/bin/bash
# Serialized dotnet build. Several agents share one workspace; MSBuild writes to
# shared obj/bin folders, so builds must not overlap. First argument is the target
# project/solution; remaining arguments are forwarded to dotnet build.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
source scripts/dev-env.sh
if [ $# -lt 1 ]; then echo "usage: scripts/build.sh <project|solution> [dotnet build args...]" >&2; exit 2; fi
TARGET="$1"; shift
LOCK="$ROOT/.msbuild-lock.d"
tries=0
while ! mkdir "$LOCK" 2>/dev/null; do
  tries=$((tries+1))
  if [ $((tries % 120)) -eq 0 ]; then echo "waiting for build lock ($LOCK)..." >&2; fi
  sleep 0.5
done
trap 'rmdir "$LOCK" 2>/dev/null || true' EXIT
dotnet build "$TARGET" "$@"
