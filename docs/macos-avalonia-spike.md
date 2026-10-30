# Avalonia 11.3.22 on macOS — feasibility spike (for the WPF → Avalonia port)

> **Note for repository readers:** this is the captured report of a local feasibility spike. The
> throwaway spike project itself (`spikes/avalonia-macos-smoke/`, including its smoke app and test
> project) is not committed (it is gitignored), so the `spikes/...` paths below are historical. The
> two reusable results were productionised: the headless rendering/capture recipe is implemented in
> `src/BandcampDownloader.Avalonia.Tests/` (`TestAppBuilder`, `Screenshots`) and the packaging recipe
> is implemented in `scripts/package-macos.sh` + `packaging/macos/`. Everything else (versions,
> pitfalls, commands) is valid as recorded on macOS 27.0.1 arm64.

Status: **all 4 questions answered, all positive**, with the caveats called out below.
Everything here was actually built and run locally on 2026-10-30.

Environment: macOS **27.0.1** arm64 (Apple Silicon), .NET SDK **10.0.403** from `./.dotnet`,
Avalonia **11.3.22**, workspace `/path/to/Bandcamp`.
Artifacts were created locally under the (gitignored) `spikes/avalonia-macos-smoke/` directory.

| # | Question | Verdict |
|---|----------|---------|
| 1 | Minimal Avalonia 11.3.22 net10.0 app builds with repo SDK/scripts | ✅ build succeeds, 0 warnings |
| 2 | Real GUI window launches from this agent session | ✅ yes (window visible on the actual desktop, screenshot proof) |
| 3 | Headless render → PNG (visual verification without a display) | ✅ yes, console app **and** NUnit `[AvaloniaTest]` both produce correct non-blank PNGs |
| 4 | macOS `.app` packaging (publish + icns + ad-hoc sign + `open`) | ✅ yes, `open` → alive 5s+, ad-hoc signature valid |

---

## 1) Build — Avalonia 11.3.22 net10.0

**Files** (`spikes/avalonia-macos-smoke/`): `AvaloniaSmoke.csproj`, `Program.cs`, `App.axaml(.cs)`,
`MainWindow.axaml(.cs)`, `app.manifest`.

**Command (works):**

```bash
source scripts/dev-env.sh
scripts/build.sh spikes/avalonia-macos-smoke/AvaloniaSmoke.csproj
# -> Build succeeded. 0 Warning(s) 0 Error(s)
# resolved: Avalonia/11.3.22, Avalonia.Desktop/11.3.22, Avalonia.Themes.Fluent/11.3.22
```

csproj essentials:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.22" />
    <PackageReference Include="Avalonia.Desktop" Version="11.3.22" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.22" />
  </ItemGroup>
</Project>
```

Notes:
- **NuGet audit: no warnings observed.** `src/Directory.Build.props` (which sets
  `TreatWarningsAsErrors`) does **not** apply to `spikes/` — there is no root `Directory.Build.props`
  and `src/global.json` only affects commands run under `src/`. Build output had 0 warnings, so no
  NuGet audit `NU190x` issue with Avalonia 11.3.22 today.
- Transitive graphics stack resolved: `Avalonia.Skia/11.3.22`, `SkiaSharp/2.88.9`,
  `HarfBuzzSharp/8.3.1.1` (macOS native assets included by `Avalonia.Desktop`).
- **Framework-dependent output needs `DOTNET_ROOT`**: running `bin/Debug/net10.0/AvaloniaSmoke`
  without `source scripts/dev-env.sh` prints "You must install .NET… Not found". Self-contained
  publish output does not need it.
- `app.manifest` is Windows-only and irrelevant on macOS; harmless to keep if ports share a file.

---

## 2) Can a real GUI window launch from this agent session? — YES

```bash
source scripts/dev-env.sh
cd spikes/avalonia-macos-smoke
bin/Debug/net10.0/AvaloniaSmoke >out/gui/stdout.log 2>out/gui/stderr.log &   # pid alive after 6s
```

Evidence gathered while it was running:
- Process stayed alive >6 s, empty stdout/stderr.
- In-app diagnostics (window `Opened` + first dispatcher pass):
  `IsVisible=True Bounds=480x320 Screens=1 Primary=1512x982@1 PlatformImpl=WindowImpl`
  → the macOS native (Avalonia.Native) backend created a real window on the real display.
- `lsappinfo list` showed the process registered with LaunchServices as
  `type="Foreground" ... (in front)`, i.e. a real foreground GUI app checked in with WindowServer.
- `screencapture -x` produced a 3024×1964 retina PNG in which the Avalonia window is visibly
  rendering text/button: `out/gui/screen-capture.png` (also `out/gui/desktop-render.png`, a
  `RenderTargetBitmap` of the live window saved by the app itself).
- `osascript` **cannot** inspect windows: `System Events got an error: A privilege violation
  occurred. (-10004)` (automation permission denied) — use screenshots, not AppleScript.

Caveats (be explicit with the team):
- This session runs **inside the user's Aqua GUI session**, which is why GUI launch works. Under
  launchd/SSH/CI there is no WindowServer connection and the app will abort; only headless is
  portable.
- `screencapture` was **flaky on first use**: first call returned
  `could not create image from display` (exit 1, likely macOS Screen Recording / TCC grant
  latency); every later call succeeded (3/3). Treat screen capture as supplementary evidence,
  not a CI mechanism. Also: full-desktop captures (~2–3 MB) contain unrelated user screen content —
  do not commit or share them casually.

Artifacts (kept): `out/gui/screen-capture.png`, `out/gui/desktop-render.png`, `out/gui/stderr.log`.

---

## 3) Headless rendering → PNG — **THE RECIPE** (most important for the team)

Packages (exact, all `11.3.22`): `Avalonia`, `Avalonia.Headless`, `Avalonia.Skia`,
`Avalonia.Themes.Fluent` (+ app project reference for the real `App`/windows).
`Avalonia.Headless.NUnit` only for the NUnit variant. **Do not use `Avalonia 12.x`.**

### 3a. Console app that renders the real window and saves a PNG (verified)

`spikes/avalonia-macos-smoke/headless/HeadlessCapture.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.22" />
    <PackageReference Include="Avalonia.Headless" Version="11.3.22" />
    <PackageReference Include="Avalonia.Skia" Version="11.3.22" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.22" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../AvaloniaSmoke.csproj" />
  </ItemGroup>
</Project>
```

Core code (full working file: `headless/Program.cs`):

```csharp
using Avalonia;
using Avalonia.Headless;
using Ava = Avalonia;

var builder = AppBuilder.Configure<AvaloniaSmoke.App>()
    .UseSkia()                                              // REQUIRED: real rasterizer
    .UseHeadless(new AvaloniaHeadlessPlatformOptions
    {
        UseHeadlessDrawing = false                          // REQUIRED: not the no-op drawing
    });

builder.SetupWithoutStarting();                             // starts Avalonia.UIThread / platform

var window = new AvaloniaSmoke.MainWindow();
window.Show();
Ava.Threading.Dispatcher.UIThread.RunJobs();                // let layout + first render pass run

using var frame = window.CaptureRenderedFrame();            // extension in Avalonia.Headless
// frame is WriteableBitmap?; throws NotSupportedException if Skia/headless-drawing misconfigured
frame!.Save("spikes/avalonia-macos-smoke/out/headless.png");
```

Run:

```bash
source scripts/dev-env.sh
# from repo root; default output path = spikes/avalonia-macos-smoke/out/headless.png
# (if other agents may be building concurrently, build under the shared lock first,
#  then run without rebuilding:)
scripts/build.sh spikes/avalonia-macos-smoke/headless/HeadlessCapture.csproj
dotnet run --project spikes/avalonia-macos-smoke/headless -c Debug --no-build
# or simply:  dotnet run --project spikes/avalonia-macos-smoke/headless -c Debug
# dark theme:  ... -- spikes/avalonia-macos-smoke/out/headless-dark.png --dark
# no-skia negative control: ... -- /tmp/x.png --no-skia
```

Verified output: `out/headless.png` = **480×320 PNG, 15,247 bytes, 418 distinct colors,
23.0% non-uniform pixels**, and visually correct (text + button + themed background).
`--dark` produces `RequestedThemeVariant=Dark` output (`out/headless-dark.png`, corner `#000000`).

Pitfalls / facts established empirically:
- With `UseHeadlessDrawing = true` (default) and **no** `.UseSkia()`, `CaptureRenderedFrame()`
  throws: `System.NotSupportedException: To capture a rendered frame, make sure that headless
  application was initialized with '.UseSkia()' and disabled 'UseHeadlessDrawing' in the
  'AvaloniaHeadlessPlatformOptions'.` (negative control run: exit code 134).
- Headless platform defaults: one screen `1920x1280@1`; **light theme**, because there is no
  macOS dark-mode detection headlessly — force `Application.Current.RequestedThemeVariant =
  ThemeVariant.Dark` (or `RequestedThemeVariant="Dark"` in App.axaml) when testing themes.
- Text renders correctly with Skia using macOS system fonts (no font embedding needed for en-US).
  (CJK/emoji fallback not tested.)
- **`App.OnFrameworkInitializationCompleted()` IS called in headless mode, but with
  `ApplicationLifetime == null`.** Desktop-only wiring (e.g. `if (ApplicationLifetime is
  IClassicDesktopStyleApplicationLifetime) desktop.MainWindow = new MainWindow()`) therefore does
  not run. Tests must construct windows/services themselves, and app startup code must tolerate a
  null lifetime. (Verified in both console and NUnit runs.)
- `window.Show()` + `Dispatcher.UIThread.RunJobs()` is enough; no manual render-timer ticking
  needed (`CaptureRenderedFrame()` triggers a tick internally). `AvaloniaHeadlessPlatform
  .ForceRenderTimerTick(int)` exists if more control is needed.
- Other useful extensions in `Avalonia.Headless`: `KeyPress/KeyRelease/KeyTextInput`,
  `MouseDown/MouseMove/MouseUp/MouseWheel`, `DragDrop` — UI interaction can be simulated.

### 3b. NUnit headless tests (verified, 2/2 passing) — copy-paste recipe

Project: `spikes/avalonia-macos-smoke/headless-tests/HeadlessTests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Headless.NUnit" Version="11.3.22" />
    <PackageReference Include="Avalonia.Skia" Version="11.3.22" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.0" />
    <PackageReference Include="NUnit" Version="4.6.1" />
    <PackageReference Include="NUnit3TestAdapter" Version="6.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../AvaloniaSmoke.csproj" />
  </ItemGroup>
</Project>
```

Assembly-level setup (exactly one `AvaloniaTestApplication` attribute per test assembly):

```csharp
using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;

[assembly: AvaloniaTestApplication(typeof(MyApp.Tests.TestAppBuilder))]

namespace MyApp.Tests;
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
```

Test body — attribute and namespace details matter:

```csharp
using Avalonia.Controls;
using Avalonia.Headless;          // CaptureRenderedFrame (HeadlessWindowExtensions)
using Avalonia.Headless.NUnit;    // [AvaloniaTest]  <- NOT Avalonia.Headless!
using NUnit.Framework;

public class MainWindowRenderTests
{
    [AvaloniaTest]                // NOT [Test] for anything touching Avalonia
    public void MainWindow_Renders_To_Png()
    {
        var window = new MainWindow();
        window.Show();
        var frame = window.CaptureRenderedFrame();
        Assert.That(frame, Is.Not.Null);
        frame!.Save("/abs/path/out/headless-nunit.png");
    }
}
```

Run:

```bash
source scripts/dev-env.sh
scripts/test.sh spikes/avalonia-macos-smoke/headless-tests/HeadlessTests.csproj
# -> Passed! Failed: 0, Passed: 2, Total: 2   (writes out/headless-nunit.png, 480x320, 15,247 bytes)
```

Pitfalls:
- `[AvaloniaTest]` lives in `Avalonia.Headless.NUnit`; `CaptureRenderedFrame()` lives in
  `Avalonia.Headless` — you normally need **both** usings.
- `Avalonia.Headless.NUnit/11.3.22` declares a dependency on **NUnit 3.13**, but it works fine
  against **NUnit 4.6.1** (repo's current version) at runtime; we ran it.
- Headless test session is not parallel-safe; keep all `[AvaloniaTest]` tests in one collection
  (default NUnit behavior worked for 2 tests). Plain non-UI `[Test]`s are fine in the same assembly.
- The assembly attribute must reference a **static class with `public static AppBuilder
  BuildAvaloniaApp()`**.
- Writes to the test output dir are relative to `TestContext.CurrentContext.TestDirectory`
  (`bin/...`), not the repo — use an absolute/repo-root path for artifacts you want to keep.

---

## 4) macOS `.app` packaging — works, with a reusable script

Reusable script: **`spikes/avalonia-macos-smoke/package.sh`** (`--launch`, `--no-publish` flags,
takes the same `.msbuild-lock.d` build lock around `dotnet publish`).
Top-of-file variables to adapt for the real app: `APP_NAME`, `PROJ`, `BUNDLE_ID`, `VERSION`,
`ICON_SRC`.

What it does, and the exact verified steps:

```bash
# 1. publish (note: -p:PublishSingleFile=false; self-contained osx-arm64)
source scripts/dev-env.sh
dotnet publish spikes/avalonia-macos-smoke/AvaloniaSmoke.csproj \
  -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=false -p:DebugType=embedded -o spikes/avalonia-macos-smoke/dist/publish

# 2. assemble the bundle (all managed dlls + native dylibs next to the apphost)
APP=spikes/avalonia-macos-smoke/dist/AvaloniaSmoke.app
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R spikes/avalonia-macos-smoke/dist/publish/. "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/AvaloniaSmoke"

# 3. Info.plist  (full file in package.sh; keys required: CFBundleExecutable, CFBundleIdentifier,
#    CFBundleName, CFBundlePackageType=APPL, LSMinimumSystemVersion=15.0; plus
#    CFBundleIconFile=icon.icns, CFBundleShortVersionString, NSHighResolutionCapable)

# 4. icon.icns  (iconutil needs this exact iconset naming: icon_16x16.png ... icon_512x512@2x.png)
#    Repo icon available: src/BandcampDownloader/Resources/Cloud.png = 512x512  <-- BEST SOURCE
#    (docs/images/Cloud.png is only 51x32; package.sh falls back to it)
sips -Z 1024 Cloud.png --out scaled.png
sips --padToHeightWidth 1024 1024 --padColor FFFFFF scaled.png --out master.png
for s in 16 32 128 256 512; do
  sips -z $s $s master.png --out Icon.iconset/icon_${s}x${s}.png
  sips -z $((s*2)) $((s*2)) master.png --out Icon.iconset/icon_${s}x${s}@2x.png
done
iconutil -c icns Icon.iconset -o "$APP/Contents/Resources/icon.icns"

# 5. ad-hoc code signing (Apple Silicon requires *some* signature on every Mach-O)
find "$APP/Contents/MacOS" -type f \( -name '*.dylib' -o -name '*.so' \) -print0 |
  while IFS= read -r -d '' lib; do codesign --force --sign - "$lib"; done
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"        # OK

# 6. launch check
open "$APP" && sleep 5 && pgrep -f "$APP/Contents/MacOS/AvaloniaSmoke"
```

Verified results:
- `dist/AvaloniaSmoke.app` = **109 MB** (self-contained .NET 10 runtime + Avalonia native libs).
- `codesign -dv`: `Format=app bundle with Mach-O thin (arm64)`, `Signature=adhoc`,
  `Sealed Resources version=2 rules=13 files=217`, `Info.plist entries=13`.
- `codesign --verify --deep --strict`: "valid on disk", "satisfies its Designated Requirement".
- `open dist/AvaloniaSmoke.app` → process alive after 5 s; LaunchServices shows
  `bundleID="com.example.avalonia-smoke" ... type="Foreground" (in front)`;
  screenshot shows the packaged app's window: `out/gui/packaged-app-named.png`.
- Publish output layout: single flat folder — apphost `AvaloniaSmoke` + ~200 managed `.dll`s +
  native `libAvaloniaNative.dylib`, `libSkiaSharp.dylib`, `libHarfBuzzSharp.dylib`,
  `libcoreclr.dylib`, `libclrjit.dylib`, `libclrgc.dylib`, `libSystem.Native.dylib`, … Put the
  whole folder in `Contents/MacOS/` (simplest; apphost resolves `<AssemblyName>.dll` next to itself).

### Packaging pitfalls (all encountered/verified)

1. **macOS menu bar shows "Avalonia Application" instead of the app name.** `CFBundleName` in
   Info.plist alone is NOT enough (verified: first packaged app had `CFBundleName=AvaloniaSmoke`
   but the menu still read "Avalonia Application"). The hard-coded default lives as a UTF-16
   string in `Avalonia.Controls.dll`. **Fix: set `Application.Name`** (e.g. in
   `App.Initialize()`): `Name = "BandcampDownloader";` — after that the menu bar shows the app
   name (verified via screenshot + `lsappinfo`). Window titles are unaffected.
2. **Ad-hoc signed ≠ Gatekeeper-approved.** `spctl --assess --type execute` exits 1 (no usable
   signature/not notarized). Locally built bundles have no `com.apple.quarantine` xattr and open
   fine. If you distribute a zip/dmg, users must `xattr -dr com.apple.quarantine App.app` or
   right-click → Open; real distribution needs a Developer ID cert + notarization (**untested here,
   out of scope**).
3. **Framework-dependent vs self-contained.** `dotnet publish --self-contained` output runs with no
   .NET installed; a framework-dependent build requires `DOTNET_ROOT` / a system .NET 10 install
   (the Debug apphost failed with "You must install .NET" when `scripts/dev-env.sh` wasn't sourced).
4. **`CFBundleExecutable` must exactly equal the apphost binary name** (= MSBuild `AssemblyName`).
   Keep bundle/asset names ASCII; quote paths in scripts (spaces fine).
5. `codesign --deep` is deprecated but works for ad-hoc; signing every dylib explicitly first is
   more robust and is what the script does. `--force` is needed to re-sign an already-assembled app.
6. **`open` discards stdout/stderr** — for diagnostics either run
   `Contents/MacOS/<exe>` directly with `DOTNET_ROOT` set, or have the app log to a file.
7. **`dotnet publish` uses the same shared `obj/bin` as the other agents** — always take
   `.msbuild-lock.d` (package.sh does, same protocol as `scripts/build.sh`). Never run parallel
   `dotnet build/publish` in this workspace.
8. `LSMinimumSystemVersion`: .NET 10's currently supported macOS floor is **15** (see
   [dotnet/core supported-os 10.0](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md);
   macOS 14 and below are listed as out of support/not tested). package.sh therefore declares
   `15.0`. The app itself ran fine on this macOS 27.0.1 machine; older macOS was not tested here.
9. Icon quality: `docs/images/Cloud.png` is 51×32 (icon will be blurry). Use
   `src/BandcampDownloader/Resources/Cloud.png` (**512×512**) — package.sh prefers it
   automatically. A 1024×1024 source would be ideal.

---

## Cross-cutting warnings for the team

- **Nested-project glob trap (hit for real):** the app project at `spikes/avalonia-macos-smoke/`
  default-globs `**/*.cs`, so sibling spike folders with their own `Program.cs` caused
  `CS0017: Program has more than one entry point defined`. Fix used: `<Compile Remove="headless/**" />`
  etc. in the app csproj. If headless test projects are placed *inside* the Avalonia app project
  folder, the same exclusion (or a sibling layout) is required.
- **Avalonia 11.3.22 works on macOS arm64 + net10.0** — no need for Avalonia 12 previews.
- **Visual verification strategy we recommend:** use the headless console/NUnit recipe (3a/3b) for
  deterministic PNGs (can be diffed, committed small, run with no display); use `screencapture`
  only as a manual sanity check (needs Screen Recording permission in the host app, full-desktop
  images are large/privacy-sensitive, first call can fail).
- `spikes/` is currently **untracked** and not in `.gitignore`; artifacts include a 109 MB
  `dist/` plus `bin/obj` (those patterns are already ignored). Decide with the Lead whether to
  gitignore the whole `spikes/` dir or keep the small sources tracked.
- Headless app startup must tolerate `ApplicationLifetime == null` (see 3a). If the real Avalonia
  app creates DI/services in `OnFrameworkInitializationCompleted` only for the desktop lifetime,
  headless tests will not get them — factor startup so tests can instantiate the app window.

## Files created

```
spikes/avalonia-macos-smoke/
├── AvaloniaSmoke.csproj / Program.cs / App.axaml(.cs) / MainWindow.axaml(.cs) / app.manifest
├── package.sh                       # reusable: publish -> .app -> icns -> ad-hoc sign -> verify -> optional open
├── headless/
│   ├── HeadlessCapture.csproj       # packages: Avalonia.Headless + Avalonia.Skia 11.3.22
│   └── Program.cs                   # headless render + PNG save + non-blank pixel check (--dark, --no-skia)
├── headless-tests/
│   ├── HeadlessTests.csproj         # Avalonia.Headless.NUnit 11.3.22 + NUnit 4.6.1
│   ├── TestAppBuilder.cs            # [assembly: AvaloniaTestApplication] setup
│   └── MainWindowRenderTests.cs     # [AvaloniaTest] render-to-PNG + simulated button click
├── out/
│   ├── headless.png                 # 480x320 headless render (light)
│   ├── headless-dark.png            # 480x320 headless render (dark)
│   ├── headless-nunit.png           # PNG written by the NUnit test
│   └── gui/
│       ├── screen-capture.png       # real desktop screenshot of the live Debug window
│       ├── desktop-render.png       # RenderTargetBitmap from the live window
│       ├── packaged-app.png / packaged-app-named.png   # .app running (before/after Application.Name fix)
│       └── stderr.log               # in-app window/render diagnostics
└── dist/
    ├── publish/                     # dotnet publish output (self-contained osx-arm64)
    └── AvaloniaSmoke.app            # assembled, ad-hoc signed bundle (109 MB, gitignored via dist/)
```
