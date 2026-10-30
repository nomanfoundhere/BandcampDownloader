#!/bin/bash
# Installs the .NET SDK used by this repository into ./.dotnet (no admin rights required), so that
# the whole toolchain is self-contained and nothing is written outside the repository.
#
# Usage: scripts/install-dotnet.sh [channel]   # channel defaults to 10.0
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHANNEL="${1:-10.0}"
INSTALL_DIR="$ROOT/.dotnet"

if [ -x "$INSTALL_DIR/dotnet" ]; then
    echo ".NET SDK already installed in $INSTALL_DIR ($("$INSTALL_DIR/dotnet" --version))"
    exit 0
fi

TMP_SCRIPT="$(mktemp)"
trap 'rm -f "$TMP_SCRIPT"' EXIT

curl -sSL https://dot.net/v1/dotnet-install.sh -o "$TMP_SCRIPT"
chmod +x "$TMP_SCRIPT"
"$TMP_SCRIPT" --channel "$CHANNEL" --install-dir "$INSTALL_DIR"

echo "Installed .NET SDK: $("$INSTALL_DIR/dotnet" --version)"
echo "Run: source scripts/dev-env.sh"
