#!/bin/bash
# scripts/package-macos.sh -- build the macOS application bundle (Avalonia head).
#
# Produces, in <repo>/dist:
#   Bandcamp Downloader.app                              ad-hoc-signed, self-contained .app
#   BandcampDownloader-<version>-macOS-<arch>.zip        shareable/CI zip of the .app
#
# The zip is what .github/workflows/macos.yml uploads as the workflow artifact (dist/*.zip).
#
# Usage:
#   scripts/package-macos.sh                       # osx-arm64 (Apple Silicon) bundle + zip
#   scripts/package-macos.sh --rid osx-x64         # Intel bundle + zip (cross-compiled)
#   scripts/package-macos.sh --launch              # package + smoke-verify (Finder launch 5s
#                                                 # liveness, then temp-data-dir data files)
#   scripts/package-macos.sh --publish-dir DIR     # bundle an existing publish output
#   scripts/package-macos.sh --no-publish          # reuse dist/publish/<rid> from a previous run
#   scripts/package-macos.sh --version 1.5.2       # override the version read from Directory.Build.props
#   scripts/package-macos.sh --output /tmp/out     # bundle output directory (default: <repo>/dist)
#   scripts/package-macos.sh -h | --help
#
# Environment overrides (useful for forks / CI):
#   MACOS_RID, MACOS_APP_NAME, MACOS_EXECUTABLE_NAME, MACOS_BUNDLE_ID, MACOS_DIST_DIR,
#   MACOS_ICON_SOURCE, MACOS_MIN_MACOS, MACOS_APP_PROJECT, MACOS_COPYRIGHT
#
# Requirements: macOS (sips, iconutil, codesign, ditto, open, plutil are used) and a `dotnet`
# CLI on PATH. scripts/dev-env.sh is sourced first: it uses the in-repo SDK (./.dotnet, installed
# by scripts/install-dotnet.sh) when present and the system `dotnet` otherwise, so the same script
# works locally and on GitHub Actions' macOS runners. `dotnet publish` takes the shared
# .msbuild-lock.d lock used by scripts/build.sh because several agents may build concurrently.
#
# Distributing the result: the bundle is ad-hoc signed only (not notarized). It runs locally;
# when downloaded from the internet, Gatekeeper asks the user to allow it (see docs/macos.md).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# shellcheck source=scripts/dev-env.sh
source scripts/dev-env.sh

# ---------------------------------------------------------------- configuration
APP_PROJECT="${MACOS_APP_PROJECT:-src/BandcampDownloader.Avalonia/BandcampDownloader.Avalonia.csproj}"
APP_NAME="${MACOS_APP_NAME:-Bandcamp Downloader}"          # .app folder + CFBundleName/DisplayName
EXECUTABLE_NAME="${MACOS_EXECUTABLE_NAME:-BandcampDownloader}"  # binary in Contents/MacOS
BUNDLE_ID="${MACOS_BUNDLE_ID:-com.otiel.bandcampdownloader.mac}"
MIN_MACOS="${MACOS_MIN_MACOS:-15.0}"                       # .NET 10's supported macOS floor
ICON_SOURCE="${MACOS_ICON_SOURCE:-src/BandcampDownloader.Avalonia/Assets/Cloud.png}"
DIST_DIR="${MACOS_DIST_DIR:-$ROOT/dist}"
PLIST_TEMPLATE="$ROOT/packaging/macos/Info.plist.in"
VERSION=""

RID="osx-arm64"
LAUNCH=0
DO_PUBLISH=1
PUBLISH_DIR=""

usage() {
    sed -n '2,/^set -euo pipefail$/s/^# \{0,1\}//p' "$0" | sed -e :a -e '/^\n*$/{$d;N;ba' -e '}'
}

while [ $# -gt 0 ]; do
    case "$1" in
        --rid)          RID="${2:?--rid needs a value}"; shift 2 ;;
        --rid=*)        RID="${1#--rid=}"; shift ;;
        --version)      VERSION="${2:?--version needs a value}"; shift 2 ;;
        --version=*)    VERSION="${1#--version=}"; shift ;;
        --output)       DIST_DIR="${2:?--output needs a value}"; shift 2 ;;
        --output=*)     DIST_DIR="${1#--output=}"; shift ;;
        --publish-dir)  PUBLISH_DIR="${2:?--publish-dir needs a value}"; DO_PUBLISH=0; shift 2 ;;
        --publish-dir=*) PUBLISH_DIR="${1#--publish-dir=}"; DO_PUBLISH=0; shift ;;
        --no-publish)   DO_PUBLISH=0; shift ;;
        --launch)       LAUNCH=1; shift ;;
        -h|--help)      usage; exit 0 ;;
        *) echo "error: unknown argument '$1' (see --help)" >&2; exit 2 ;;
    esac
done

case "$RID" in
    osx-arm64|osx-x64) ;;
    universal|osx-universal)
        echo "error: universal bundles are not produced; run once with --rid osx-arm64 and once with" >&2
        echo "       --rid osx-x64 instead (see packaging/macos/README.md)." >&2
        exit 2 ;;
    *)
        echo "error: unsupported RID '$RID' for a macOS bundle (expected osx-arm64 or osx-x64)." >&2
        exit 2 ;;
esac

if [ -z "$VERSION" ]; then
    VERSION="$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' src/Directory.Build.props | head -n 1)"
fi
if [ -z "$VERSION" ]; then
    echo "error: could not read <VersionPrefix> from src/Directory.Build.props (use --version)." >&2
    exit 2
fi

COPYRIGHT="${MACOS_COPYRIGHT:-}"
if [ -z "$COPYRIGHT" ]; then
    COPYRIGHT="$(sed -n 's:.*<Copyright>\(.*\)</Copyright>.*:\1:p' src/Directory.Build.props src/BandcampDownloader.Avalonia/BandcampDownloader.Avalonia.csproj 2>/dev/null | head -n 1)"
    # MSBuild expressions (e.g. "$([System.DateTime]::Now.Year)") are evaluated at build time only.
    COPYRIGHT="$(printf '%s' "$COPYRIGHT" | sed "s/\$(\[System[.]DateTime\]::Now[.]Year)/$(date +%Y)/")"
fi
COPYRIGHT="${COPYRIGHT:-© Otiel}"

# Archive name convention: BandcampDownloader-<version>-macOS-<arch>.zip (CI uploads dist/*.zip).
ARCH="${RID#osx-}"
ARCHIVE_NAME="BandcampDownloader-$VERSION-macOS-$ARCH.zip"
ARCHIVE="$DIST_DIR/$ARCHIVE_NAME"

APP="$DIST_DIR/$APP_NAME.app"
if [ -z "$PUBLISH_DIR" ]; then
    PUBLISH_DIR="$DIST_DIR/publish/$RID"
fi

for tool in dotnet sips iconutil codesign ditto shasum plutil open pgrep; do
    command -v "$tool" >/dev/null 2>&1 || { echo "error: '$tool' not found (macOS + .NET required)." >&2; exit 1; }
done
test -f "$PLIST_TEMPLATE" || { echo "error: missing Info.plist template: $PLIST_TEMPLATE" >&2; exit 2; }

echo "==> Project:  $APP_PROJECT"
echo "==> Bundle:   $APP"
echo "==> Version:  $VERSION (RID $RID, minimum macOS $MIN_MACOS)"

# ---------------------------------------------------------------- 1. publish
if [ "$DO_PUBLISH" = 1 ]; then
    echo "==> dotnet publish (Release, self-contained, $RID)"
    LOCK="$ROOT/.msbuild-lock.d"
    tries=0
    while ! mkdir "$LOCK" 2>/dev/null; do
        tries=$((tries+1))
        if [ $((tries % 120)) -eq 0 ]; then echo "    waiting for build lock ($LOCK)..." >&2; fi
        sleep 0.5
    done
    trap 'rmdir "$LOCK" 2>/dev/null || true' EXIT

    rm -rf "$PUBLISH_DIR"
    mkdir -p "$PUBLISH_DIR"
    dotnet publish "$APP_PROJECT" \
        -c Release \
        -r "$RID" \
        --self-contained true \
        -p:PublishSingleFile=false \
        -o "$PUBLISH_DIR"

    rmdir "$LOCK" 2>/dev/null || true
    trap - EXIT
fi

if [ ! -d "$PUBLISH_DIR" ]; then
    echo "error: publish directory not found: $PUBLISH_DIR (run without --publish-dir/--no-publish first)." >&2
    exit 1
fi

# The apphost produced by `dotnet publish` is named after the project's assembly name (the .csproj
# file name unless <AssemblyName> is set). We rename it to EXECUTABLE_NAME in Contents/MacOS: the
# .NET apphost embeds the managed assembly name, so the rename is safe (verified in spike task-3).
PROJECT_EXE="$(basename "$APP_PROJECT" .csproj)"
if [ ! -f "$PUBLISH_DIR/$PROJECT_EXE" ]; then
    echo "error: apphost '$PROJECT_EXE' not found in $PUBLISH_DIR." >&2
    echo "       (set MACOS_APP_PROJECT/MACOS_EXECUTABLE_NAME, or check the project's AssemblyName.)" >&2
    exit 1
fi

# ---------------------------------------------------------------- 2. assemble bundle
echo "==> Assembling the bundle"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
if [ "$PROJECT_EXE" != "$EXECUTABLE_NAME" ]; then
    mv "$APP/Contents/MacOS/$PROJECT_EXE" "$APP/Contents/MacOS/$EXECUTABLE_NAME"
fi
chmod +x "$APP/Contents/MacOS/$EXECUTABLE_NAME"

# Info.plist
sed -e "s|__APP_NAME__|$APP_NAME|g" \
    -e "s|__EXECUTABLE_NAME__|$EXECUTABLE_NAME|g" \
    -e "s|__BUNDLE_ID__|$BUNDLE_ID|g" \
    -e "s|__VERSION__|$VERSION|g" \
    -e "s|__MIN_MACOS__|$MIN_MACOS|g" \
    -e "s|__COPYRIGHT__|$COPYRIGHT|g" \
    "$PLIST_TEMPLATE" > "$APP/Contents/Info.plist"
plutil -lint "$APP/Contents/Info.plist"

# Icon: the source PNG is 512x512; scale it to 1024 then (for non-square sources) pad it to a
# square canvas. iconutil requires exactly these file names.
echo "==> Generating icon.icns from $ICON_SOURCE"
test -f "$ICON_SOURCE" || { echo "error: icon source not found: $ICON_SOURCE" >&2; exit 1; }
ICON_WORK="$DIST_DIR/.icon-work"
rm -rf "$ICON_WORK"
mkdir -p "$ICON_WORK/Icon.iconset"
MASTER="$ICON_WORK/master-1024.png"
sips -Z 1024 "$ICON_SOURCE" --out "$ICON_WORK/scaled.png" >/dev/null 2>&1
sips --padToHeightWidth 1024 1024 --padColor FFFFFF "$ICON_WORK/scaled.png" --out "$MASTER" >/dev/null 2>&1
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$MASTER" --out "$ICON_WORK/Icon.iconset/icon_${size}x${size}.png" >/dev/null 2>&1
    sips -z $((size*2)) $((size*2)) "$MASTER" --out "$ICON_WORK/Icon.iconset/icon_${size}x${size}@2x.png" >/dev/null 2>&1
done
iconutil -c icns "$ICON_WORK/Icon.iconset" -o "$APP/Contents/Resources/icon.icns"
test -s "$APP/Contents/Resources/icon.icns" || { echo "error: icon.icns generation failed." >&2; exit 1; }
rm -rf "$ICON_WORK"

# Licence and user documentation inside the bundle.
cp "$ROOT/LICENSE" "$APP/Contents/Resources/LICENSE"
if [ -f "$ROOT/docs/macos.md" ]; then
    cp "$ROOT/docs/macos.md" "$APP/Contents/Resources/README.md"
fi

# ---------------------------------------------------------------- 3. ad-hoc code signing
# On Apple Silicon every Mach-O file must carry at least an ad-hoc signature. Sign the nested
# dylibs first (more robust than relying on --deep alone), then the bundle as a whole.
echo "==> Ad-hoc code signing"
SIGN_FAILED=0
while IFS= read -r -d '' lib; do
    if ! codesign --force --sign - "$lib" >/dev/null 2>&1; then
        echo "    warning: could not sign $lib" >&2
        SIGN_FAILED=1
    fi
done < <(find "$APP/Contents/MacOS" -type f \( -name '*.dylib' -o -name '*.so' \) -print0)
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
[ "$SIGN_FAILED" -eq 0 ] || { echo "error: some nested libraries could not be signed." >&2; exit 1; }

# ---------------------------------------------------------------- 4. shareable .zip
# The zip (not the .app folder) is the distributable artifact and what the macOS CI workflow
# uploads. Use `ditto -c -k --keepParent` (NOT `zip -r`): it keeps ".app" as the zip root and
# preserves extended attributes. That matters here: `codesign --deep` stores detached signatures
# for the bundled managed assemblies (PE .dll files) in xattrs, and they are needed for
# `codesign --verify --deep --strict` to pass after extraction. Extract with Finder/Archive Utility
# or `ditto -x -k` (both preserve xattrs); the `unzip` CLI drops them (the app still runs, but
# codesign --verify then reports "code object is not signed at all" for the .dll files).
echo "==> Creating $ARCHIVE_NAME"
rm -f "$DIST_DIR"/BandcampDownloader-*-macOS-*.zip
( cd "$DIST_DIR" && ditto -c -k --keepParent "$APP_NAME.app" "$ARCHIVE_NAME" )
test -s "$ARCHIVE" || { echo "error: zip creation failed: $ARCHIVE" >&2; exit 1; }
unzip -l "$ARCHIVE" | grep "Contents/Info.plist" >/dev/null || {
    echo "error: zip does not contain Contents/Info.plist: $ARCHIVE" >&2; exit 1;
}
ZIP_SHA256="$(shasum -a 256 "$ARCHIVE" | awk '{print $1}')"

# Round-trip check: what users get must still be a valid, signed bundle after extraction.
# (`ditto -x -k` is the AppleDouble/xattr-preserving extractor used by Finder's Archive Utility.)
ZIP_CHECK_DIR="$DIST_DIR/.zip-check"
rm -rf "$ZIP_CHECK_DIR"
mkdir -p "$ZIP_CHECK_DIR"
if ! ditto -x -k "$ARCHIVE" "$ZIP_CHECK_DIR"; then
    echo "error: could not extract the zip for verification." >&2
    rm -rf "$ZIP_CHECK_DIR"
    exit 1
fi
if ! codesign --verify --deep --strict "$ZIP_CHECK_DIR/$APP_NAME.app"; then
    echo "error: the extracted app does not pass codesign verification." >&2
    rm -rf "$ZIP_CHECK_DIR"
    exit 1
fi
rm -rf "$ZIP_CHECK_DIR"
echo "    round-trip check OK (extracted app signature is valid)"

# ---------------------------------------------------------------- 5. summary
echo "==> OK: $APP"
echo "    version:   $VERSION"
echo "    executable: $EXECUTABLE_NAME"
echo "    size:      $(du -sh "$APP" | cut -f1)"
codesign -dv "$APP" 2>&1 | sed -e 's/^/    /' -e '/^    Executable=/d' -e '/^    Identifier=/d'
echo "    open:      open \"$APP\""
echo "    archive:   $ARCHIVE ($(du -h "$ARCHIVE" | cut -f1 | tr -d ' '))"
if [ "$RID" = "osx-x64" ]; then
    echo "    note:      x64 bundle runs on Intel Macs or on Apple Silicon through Rosetta 2."
fi

# ---------------------------------------------------------------- 6. optional launch check
# Two-phase check:
#  1. Launch through LaunchServices (`open`, the Finder-equivalent path): 5s liveness only.
#     NOTE: `open --env` does not propagate environment variables on all macOS versions (verified
#     on macOS 27.0.1), so this phase uses the standard user data directory.
#  2. Launch the bundled executable directly with a temporary BANDCAMPDOWNLOADER_DATA_DIR and
#     check that BandcampDownloader.ini and BandcampDownloader.log are written.
if [ "$LAUNCH" = 1 ]; then
    echo "==> Verifying the bundle"
    DATA_DIR="$DIST_DIR/launch-data"
    STDERR_LOG="$DIST_DIR/launch-stderr.log"
    rm -rf "$DATA_DIR" "$STDERR_LOG"
    mkdir -p "$DATA_DIR"

    echo "    [1/2] LaunchServices launch (open -n), 5s liveness"
    open -n --stderr "$STDERR_LOG" "$APP"
    sleep 5
    PIDS="$(pgrep -f "$APP/Contents/MacOS/$EXECUTABLE_NAME" || true)"
    if [ -z "$PIDS" ]; then
        echo "error: the app is not running 5s after 'open'." >&2
        [ -s "$STDERR_LOG" ] && { echo "--- stderr ---" >&2; tail -n 40 "$STDERR_LOG" >&2; }
        exit 1
    fi
    echo "          running: pid(s) $(echo "$PIDS" | tr '\n' ' ')"
    kill $PIDS 2>/dev/null || true
    sleep 1
    pkill -9 -f "$APP/Contents/MacOS/$EXECUTABLE_NAME" 2>/dev/null || true

    echo "    [2/2] Direct launch with BANDCAMPDOWNLOADER_DATA_DIR=$DATA_DIR"
    BANDCAMPDOWNLOADER_DATA_DIR="$DATA_DIR" \
        "$APP/Contents/MacOS/$EXECUTABLE_NAME" >"$DATA_DIR/stdout.log" 2>"$DATA_DIR/stderr.log" &
    APP_PID=$!
    sleep 5
    if ! kill -0 "$APP_PID" 2>/dev/null; then
        echo "error: the packaged executable exited within 5s (data dir mode)." >&2
        [ -s "$DATA_DIR/stderr.log" ] && { echo "--- stderr ---" >&2; tail -n 40 "$DATA_DIR/stderr.log" >&2; }
        exit 1
    fi
    echo "          running: pid $APP_PID"

    MISSING=""
    [ -f "$DATA_DIR/BandcampDownloader.ini" ] || MISSING="$MISSING BandcampDownloader.ini"
    [ -f "$DATA_DIR/BandcampDownloader.log" ] || MISSING="$MISSING BandcampDownloader.log"
    if [ -n "$MISSING" ]; then
        echo "error: the app did not write expected data file(s) to $DATA_DIR:$MISSING" >&2
        [ -s "$DATA_DIR/stderr.log" ] && { echo "--- stderr ---" >&2; tail -n 40 "$DATA_DIR/stderr.log" >&2; }
        kill "$APP_PID" 2>/dev/null || true
        exit 1
    fi
    echo "          data dir: $DATA_DIR (BandcampDownloader.ini + BandcampDownloader.log written)"
    if grep -qE 'Exception|FATAL' "$DATA_DIR/BandcampDownloader.log" 2>/dev/null; then
        echo "          warning: the app log contains exception/fatal lines:" >&2
        grep -E 'Exception|FATAL' "$DATA_DIR/BandcampDownloader.log" | head -n 10 >&2
    fi
    if [ -s "$DATA_DIR/stderr.log" ]; then
        echo "          stderr (non-empty):" >&2
        tail -n 20 "$DATA_DIR/stderr.log" >&2
    fi

    kill "$APP_PID" 2>/dev/null || true
    { wait "$APP_PID"; } 2>/dev/null || true   # reap; avoids a "Terminated" job message from bash
    sleep 1
    pkill -9 -f "$APP/Contents/MacOS/$EXECUTABLE_NAME" 2>/dev/null || true
    echo "==> Verified and quit the app."
fi

# ---------------------------------------------------------------- 7. artifacts
echo "==> macOS artifacts"
echo "    .app:    $APP"
echo "    zip:     $ARCHIVE"
shasum -a 256 "$ARCHIVE" | sed 's/^/    sha256:  /'
