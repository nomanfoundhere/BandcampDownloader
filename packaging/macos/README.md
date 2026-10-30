# macOS packaging

User-facing documentation lives in [`docs/macos.md`](../../docs/macos.md); this folder only holds
the templates used by [`scripts/package-macos.sh`](../../scripts/package-macos.sh).

| File | Purpose |
|---|---|
| `Info.plist.in` | `Info.plist` template for the app bundle. Placeholders (`__APP_NAME__`, `__EXECUTABLE_NAME__`, `__BUNDLE_ID__`, `__VERSION__`, `__MIN_MACOS__`, `__COPYRIGHT__`) are substituted by the packaging script. |

## What the script produces

```
dist/
├── Bandcamp Downloader.app/                          # assembled, ad-hoc signed bundle
│   └── Contents/
│       ├── MacOS/                                    # apphost + managed assemblies + native dylibs
│       ├── Resources/
│       │   ├── icon.icns
│       │   ├── LICENSE
│       │   └── README.md                             # copy of docs/macos.md
│       └── Info.plist
├── BandcampDownloader-<version>-macOS-<arch>.zip      # shareable zip (CI uploads dist/*.zip)
└── publish/<rid>/                                    # raw dotnet publish output (intermediate)
```

The zip is created with `ditto -c -k --keepParent "Bandcamp Downloader.app" …` and the run prints
both artifact paths plus the zip's `shasum -a 256`. `.github/workflows/macos.yml` uploads
`dist/*.zip` as the **BandcampDownloader-macOS** artifact, so keep the zip directly under `dist/`.

The script then extracts the zip with `ditto -x -k` into a temporary directory and runs
`codesign --verify --deep --strict` on the extracted app, so a broken zip fails the packaging run
(this happens on GitHub Actions too).

**Why `ditto` and not `zip`/`unzip`:** `codesign --deep` stores detached signatures for the bundled
managed assemblies (PE `.dll` files, which it treats as nested code) in *extended attributes*.
`ditto` preserves them, so Finder/Archive Utility extraction (and `ditto -x -k`) keeps
`codesign --verify --deep --strict` green. The command-line `unzip` tool drops extended attributes:
the app still launches, but `codesign --verify --deep --strict` then reports
`code object is not signed at all` for the `.dll` files. Users on macOS normally double-click the
zip (Archive Utility), so this only matters for scripts that use `unzip`.

## Notes / decisions

- **CI-safe**: the script sources `scripts/dev-env.sh` (which selects `./.dotnet` when installed by
  `scripts/install-dotnet.sh`, otherwise the system `dotnet`) and calls plain `dotnet publish`.
  It is executable, works from any working directory, and never writes outside `dist/` (plus the
  shared `.msbuild-lock.d` lock while publishing).
- **Self-contained** publish: recipients do not need .NET installed; the bundle is ~116 MB.
  A framework-dependent bundle would be much smaller but require a matching .NET 10 runtime, and
  our dev environment only has the in-repo SDK, so self-contained is the supported distribution.
- **Ad-hoc signature** (`codesign --sign -`): no Apple Developer account is available. The app runs
  locally and on machines where the user allows it, but it is not notarized: Gatekeeper blocks it
  when it was downloaded. See `docs/macos.md` → Known limitations for the user workarounds.
  A future signed release should use a Developer ID certificate, the hardened runtime
  (`codesign --options runtime`) and `notarytool`; the nested-dylib signing step in the script can
  stay as is before that final signing pass.
- **Universal (arm64 + x64) bundles are not produced.** .NET self-contained output is
  architecture-specific (apphost and native dylibs). The script takes `--rid osx-x64` so Intel
  builds can be made, and a universal bundle would require `lipo`-ing every Mach-O file from the
  two publishes. Not worth it until needed.
- The app is put in a folder called `Bandcamp Downloader.app` (spaces are fine; all script paths are
  quoted), while the executable inside `Contents/MacOS/` is renamed from
  `BandcampDownloader.Avalonia` to `BandcampDownloader` (safe: the .NET apphost keeps the managed
  assembly name; verified in the feasibility spike). `CFBundleExecutable` must match that file name.
