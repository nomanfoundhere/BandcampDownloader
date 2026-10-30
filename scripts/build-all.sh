#!/bin/bash
# Builds every head of the repository on macOS (the Windows WPF head is cross-compiled with
# EnableWindowsTargeting; it cannot run here but it must keep building).
#
# Usage: scripts/build-all.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
source scripts/dev-env.sh

FAIL=0

echo "=== Core (net10.0) ==="
scripts/build.sh src/BandcampDownloader.Core/BandcampDownloader.Core.csproj -v q || FAIL=1

echo "=== macOS head (Avalonia, net10.0) ==="
scripts/build.sh src/BandcampDownloader.Avalonia/BandcampDownloader.Avalonia.csproj -v q || FAIL=1

echo "=== Windows head (WPF, net10.0-windows) -> cross-compiled on macOS ==="
scripts/build.sh src/BandcampDownloader/BandcampDownloader.csproj \
    -p:EnableWindowsTargeting=true -p:RuntimeIdentifier= -p:PublishSingleFile=false -p:SelfContained=false -v q || FAIL=1

if [ -f src/BandcampDownloader.Avalonia.Tests/BandcampDownloader.Avalonia.Tests.csproj ]; then
    echo "=== Avalonia headless tests build ==="
    scripts/build.sh src/BandcampDownloader.Avalonia.Tests/BandcampDownloader.Avalonia.Tests.csproj -v q || FAIL=1
fi

echo "=== Unit tests build ==="
scripts/build.sh src/BandcampDownloader.UnitTests/BandcampDownloader.UnitTests.csproj -v q || FAIL=1

if [ "$FAIL" -ne 0 ]; then
    echo "BUILD-ALL: FAILED"
    exit 1
fi

echo "BUILD-ALL: OK"
