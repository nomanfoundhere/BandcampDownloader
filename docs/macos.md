# Bandcamp Downloader for macOS

Bandcamp Downloader is also available on macOS, as a native application built with
[Avalonia](https://avaloniaui.net/) 11.3.22 on .NET 10. It has the same feature set as the Windows
application: paste Bandcamp album/track/artist URLs, download the 128 kbps MP3 files, tag them, fetch
cover art and create playlist files.

> The Windows UI is unchanged and still ships from the
> [releases page](https://github.com/Otiel/BandcampDownloader/releases). This page only covers the
> macOS build. The shared-core improvements made for this port (per-user settings/log location,
> cross-platform path handling) also apply to the Windows build — see the
> [changelog](../CHANGELOG.md).

Current macOS build version: **1.5.2**, the same version as the Windows head (both are generated
from `VersionPrefix` in `src/Directory.Build.props`).

## Requirements

- **macOS 15 (Sequoia) or newer.** The `.app` declares `LSMinimumSystemVersion = 15.0`; .NET 10 only
  supports macOS 15+ (currently 15, 26 and 27).
- **Apple Silicon** (M1/M2/M3/M4) for the default build. Intel Macs are supported by building with
  `--rid osx-x64` (no prebuilt Intel binary is published).
- Nothing else: the packaged application is **self-contained** (~116 MB) and does not need a
  separate .NET installation.
- To build from source: a .NET 10 SDK is required. Run `scripts/install-dotnet.sh` to install one
  inside the repository (`./.dotnet`, no admin rights needed); `source scripts/dev-env.sh` then uses
  it automatically. If no in-repo SDK exists, the system `dotnet` is used instead, so the same
  scripts work on macOS CI runners. Xcode Command Line Tools are recommended for the signing tools.

## Quick start

### Run from sources (development)

```bash
cd BandcampDownloader
scripts/install-dotnet.sh                 # once: installs .NET 10 into ./.dotnet (no-op if present)
source scripts/dev-env.sh                 # uses ./.dotnet when present, else the system dotnet
scripts/build.sh src/BandcampDownloader.Avalonia/BandcampDownloader.Avalonia.csproj

# Optional: keep the settings/log out of your real profile while developing/testing:
BANDCAMPDOWNLOADER_DATA_DIR="$PWD/.appdata-run" \
  src/BandcampDownloader.Avalonia/bin/Debug/BandcampDownloader.Avalonia
```

`source scripts/dev-env.sh` is required when launching the framework-dependent development build
directly: it sets `DOTNET_ROOT` to the in-repo SDK. The packaged `.app` does not need it.

### Build the macOS application bundle

```bash
scripts/package-macos.sh                  # Apple Silicon: builds the .app and a zip, prints SHA-256
scripts/package-macos.sh --rid osx-x64    # Intel build (cross-compiled; runs through Rosetta 2
                                          # on Apple Silicon if Rosetta is installed)
open "dist/Bandcamp Downloader.app"

# Package and smoke-test in one go: launches the app through LaunchServices (Finder-equivalent)
# and checks it stays alive for 5 s, then relaunches the packaged executable with a temporary
# BANDCAMPDOWNLOADER_DATA_DIR and checks that settings + log are written.
scripts/package-macos.sh --launch
```

The script reads the version from `src/Directory.Build.props` (`VersionPrefix`, shared with the
Windows head), publishes the Avalonia project self-contained for the requested runtime identifier,
assembles `dist/Bandcamp Downloader.app`, generates `Contents/Resources/icon.icns` from
`src/BandcampDownloader.Avalonia/Assets/Cloud.png`, copies the licence and this document into
`Contents/Resources/`, ad-hoc signs the bundle, and finally creates the distributable zip:

```
dist/Bandcamp Downloader.app                              # the application
dist/BandcampDownloader-1.5.2-macOS-arm64.zip             # shareable zip (arch: arm64 or x64)
```

Both artifact paths and the zip's `shasum -a 256` are printed at the end of the run. Run
`scripts/package-macos.sh --help` for all options (`--output`, `--publish-dir`, `--no-publish`,
`--version`, environment overrides such as `MACOS_RID`).

The GitHub Actions workflow [`.github/workflows/macos.yml`](../.github/workflows/macos.yml) builds
and tests the Core and the Avalonia head, and cross-compiles the Windows WPF head (compile check
only: WPF cannot run on macOS) on a macOS runner, then runs
`scripts/package-macos.sh` and uploads `dist/*.zip` as the **BandcampDownloader-macOS** workflow
artifact. The package step is CI-safe: the script only needs a `dotnet` on `PATH` (setup-dotnet
provides it) and never assumes the in-repo SDK exists.

### Install

- From a locally built bundle: drag `dist/Bandcamp Downloader.app` into `/Applications`, or run it
  straight from `dist/`.
- From a zip (`BandcampDownloader-<version>-macOS-<arch>.zip`, e.g. a CI artifact or a release
  asset): unzip it, then drag `Bandcamp Downloader.app` into `/Applications`. Verify the SHA-256
  against the value printed by the packaging run if you want. Because the zip was downloaded, see
  the Gatekeeper note in [Known limitations](#known-limitations) for the first launch.

## Where settings, logs and downloads are stored

| What | Where |
|---|---|
| Settings | `~/Library/Application Support/BandcampDownloader/BandcampDownloader.ini` |
| Log file | `~/Library/Application Support/BandcampDownloader/BandcampDownloader.log` |
| Default downloads | `~/Music/Bandcamp/{artist}/{album}` |

- The downloads folder is configurable in **Settings → Downloads**; the `{artist}` and `{album}`
  placeholders create one folder per album.
- **Settings → Reset settings** restores the defaults (the settings file is rewritten; as in the
  Windows version, the main window and the download manager keep the values they loaded at startup,
  so the reset fully applies after a restart).
- Unlike the Windows version (which stored settings next to the executable), macOS user data lives
  in the standard per-user application-support directory.
- For portable/test runs, set `BANDCAMPDOWNLOADER_DATA_DIR` to store the `.ini` and `.log` in
  another directory instead:

  ```bash
  BANDCAMPDOWNLOADER_DATA_DIR=/tmp/bandcamp-data \
    "dist/Bandcamp Downloader.app/Contents/MacOS/BandcampDownloader"
  ```

  Launching through Finder (`open`) uses the normal user-data directory: `open --env` does not
  propagate environment variables on all macOS versions (verified not to work on macOS 27), so run
  the executable inside the bundle directly (as above) for portable/test runs. This variable is
  also what the headless UI tests use to isolate their data.

## Features (parity with the Windows application)

- Download MP3 files (128 kbps) from Bandcamp:
  - album pages: `https://[artist].bandcamp.com/album/[album]`,
  - track pages: `https://[artist].bandcamp.com/track/[track]`,
  - artist pages: `https://[artist].bandcamp.com` (optionally download the whole discography).
- Start/stop downloads, progress bar, downloaded size, download speed and live log window.
- **Incomplete Bandcamp pages are completed automatically**: when a page does not embed every MP3
  URL, the app queries Bandcamp's public API to retrieve the missing stream URLs and to find
  streamable tracks that the page data omits (for example a public excerpt on an album whose full
  track is purchase-only). Tracks that Bandcamp does not expose for streaming are skipped with a
  clear warning in the log instead of failing the whole album.
- ID3 tagging: album, artist, album artist, title, track number, year and lyrics (when available).
- Cover art: written next to the tracks and embedded in the ID3 tags.
- Playlist creation: `m3u`, `pls`, `wpl` and `zpl`.
- Settings: general (language, theme, update check), downloads (folder, file name format),
  cover art, tags, playlists, network (proxy settings are shown for parity but are not applied — see
  Known limitations), advanced (log verbosity, sounds) and about.
- The same translations as the Windows version (22 selectable languages), light and dark themes.

macOS-specific behaviour: native folder picker and menus, the application name in the menu bar,
the system sound when downloads finish, and a Dock/window icon taken from the Windows icon set.

## Known limitations

- **The application is ad-hoc signed, not notarized.** There is no Apple Developer certificate for
  this project, so when the `.app` is downloaded from the internet (zip, browser, etc.) Gatekeeper
  blocks the first launch ("Apple could not verify … is free of malware", or "… is damaged and
  can't be opened"). Allow it once:
  - **System Settings → Privacy & Security**, scroll down, click **Open Anyway** for
    Bandcamp Downloader; or
  - right-click (Control-click) the app in Finder and choose **Open**; or
  - remove the quarantine flag manually:

    ```bash
    xattr -dr com.apple.quarantine "/Applications/Bandcamp Downloader.app"
    ```

  An application built locally with `scripts/package-macos.sh` has no quarantine flag and opens
  normally.
- **No self-update.** The app can check GitHub for a newer version and links to the
  [releases page](https://github.com/Otiel/BandcampDownloader/releases), but the "Download update"
  button only opens that page in your browser: the Windows zip-based updater is not ported. Download
  a new build (or rebuild) to update.
- **Self-contained size.** The `.app` is ~116 MB because it bundles the .NET runtime.
- **Intel Macs**: a separate `--rid osx-x64` build is required; no universal binary is published.
- **ImageSharp is pinned to 3.1.12** in both heads: it is the last version under the Apache-2.0
  licence (3.2+ requires a paid commercial licence). That version has known security advisories;
  they are tracked and accepted (and suppressed in NuGet audit so the build stays green) because
  the application only decodes cover-art images downloaded from Bandcamp's own servers, never
  untrusted local image files. See the comment in `src/Directory.Build.props` for the advisory
  list. Replace ImageSharp with a maintained image stack before accepting user-supplied images.
- **No account / purchased downloads**: the app downloads the public 128 kbps streams (what can be
  listened to on the website). If you purchase a release, use Bandcamp's own download link (from
  your purchase e-mail or your Bandcamp collection) to get the full files: logging into a Bandcamp
  account is not supported by the application (same as the Windows version).
- **Windows-old behaviour that is not reproduced**: settings stored next to the executable (they
  now live in the per-user application data directory; existing Windows installations are migrated
  automatically on first start), WPF sound player (replaced by `afplay`), zip-based auto-update.
  Everything else is ported behaviour-first, not pixel-perfect.
- **Proxy settings are not applied** by either head. The Network settings section is kept for parity
  with the Windows version, but the HTTP stack does not read it (the upstream WPF implementation was
  dead code and was not wired into the downloader). Use the system proxy / VPN instead.
- The Windows WPF head still builds from `src/BandcampDownloader`; its UI code is unchanged.

## Troubleshooting

| Symptom | Fix |
|---|---|
| "Apple could not verify Bandcamp Downloader" / "app is damaged" on first launch | Ad-hoc signature + quarantine: allow it once in **System Settings → Privacy & Security → Open Anyway**, or `xattr -dr com.apple.quarantine "/Applications/Bandcamp Downloader.app"`. |
| The app does not start / closes immediately | Read `~/Library/Application Support/BandcampDownloader/BandcampDownloader.log`; crash reports are in Console.app → **Crash Reports** (`~/Library/Logs/DiagnosticReports`). Also check that the app matches your Mac's architecture (`--rid osx-arm64` vs `osx-x64`). |
| `You must install .NET to run this application` (development) | You launched `bin/Debug/BandcampDownloader.Avalonia` without the in-repo SDK on the path. Run `source scripts/dev-env.sh` first. The packaged `.app` never needs this. |
| Settings seem stuck / want a clean profile | Quit the app and delete `~/Library/Application Support/BandcampDownloader/` (or use **Settings → Reset settings**). |
| Keep test data out of your profile | Set `BANDCAMPDOWNLOADER_DATA_DIR` (see above). |
| macOS asks for access to Desktop/Documents/Downloads | Only if you point the downloads folder there; allow the folder access prompt. The default `~/Music/Bandcamp/...` needs no permission. |
| A track is reported as "not available for streaming or download" | Bandcamp does not expose a public stream for it (e.g. a purchase-only full track). The app downloads the streamable tracks of the page (for example a public excerpt) and skips the others; use Bandcamp's own download link after purchasing the release for the full files. |
| Network/proxy issues | Proxy settings are present for parity but are **not applied** by the HTTP stack (same as the Windows version): use the system proxy / a VPN instead, and check the log for errors. |
| Missing translations or a wrong theme | **Settings → General**; language and light/dark theme are applied immediately. |

## Uninstall

1. Quit the app.
2. Delete `/Applications/Bandcamp Downloader.app` (or wherever you copied it).
3. Delete `~/Library/Application Support/BandcampDownloader/` if you also want to remove settings
   and logs. Downloaded music stays where you saved it.

## For contributors

- Bootstrap the SDK (once): `scripts/install-dotnet.sh` installs it in `./.dotnet`; then
  `source scripts/dev-env.sh` for every shell.
- Build: `source scripts/dev-env.sh && scripts/build.sh src/BandcampDownloader.Avalonia/BandcampDownloader.Avalonia.csproj`
- All heads: `scripts/build-all.sh` (also cross-compiles the Windows WPF head on macOS).
- Package: `scripts/package-macos.sh` (see above); packaging details and design decisions:
  `packaging/macos/README.md`.
- CI: `.github/workflows/macos.yml` builds/tests both mac heads, packages the `.app` and uploads
  `dist/*.zip`; it uses the runner's `dotnet` (no in-repo SDK required).
- Headless UI rendering (screenshots without a display): see
  `docs/macos-avalonia-spike.md`; headless tests live in
  `src/BandcampDownloader.Avalonia.Tests` and use `BANDCAMPDOWNLOADER_DATA_DIR` for isolation.
- Implementation conventions for the port: `docs/macos-porting-notes.md`.
