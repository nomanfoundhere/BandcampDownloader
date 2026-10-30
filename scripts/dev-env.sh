#!/bin/bash
# Cross-platform dev environment for this repo.
#
# If a .NET SDK is present inside the repo (./.dotnet, installed by scripts/install-dotnet.sh),
# it is used and all dotnet/NuGet state is kept inside the workspace as well (.dotnet-home, .nuget).
# Otherwise the system `dotnet` (e.g. GitHub Actions' setup-dotnet) is used unchanged.
#
# Usage: source scripts/dev-env.sh
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ -x "$ROOT/.dotnet/dotnet" ]; then
    export DOTNET_ROOT="$ROOT/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
    export DOTNET_CLI_HOME="$ROOT/.dotnet-home"
    export NUGET_PACKAGES="$ROOT/.nuget/packages"
fi

export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
