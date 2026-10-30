# macOS port — implementation notes & conventions

BandcampDownloader is now split into a cross-platform **Core** library and two UI heads:

| Project | Target | Purpose |
|---|---|---|
| `src/BandcampDownloader.Core` | `net10.0` | All logic: Bandcamp scraping, downloading, tagging, playlists, settings, logging, updates, localization resources. **No UI, no Windows-only APIs.** |
| `src/BandcampDownloader` | `net10.0-windows` (WPF) | Original Windows UI. Kept working. |
| `src/BandcampDownloader.Avalonia` | `net10.0` (Avalonia 11.3.22) | **New macOS / cross-platform UI head.** |
| `src/BandcampDownloader.UnitTests` | `net10.0` | Tests of the Core (run on macOS). |
| `src/BandcampDownloader.Avalonia.Tests` | `net10.0` | Headless UI tests. |
| `src/BandcampDownloader.IntegrationTests` | `net10.0` | Network integration tests. |

The WPF UI files remain in `src/BandcampDownloader/UI/**` and are the reference implementation for
the Avalonia port. Port behaviour first, not pixel-perfect layout; keep the same `x:Name`s.

## Environment / build

- The .NET 10 SDK is installed **inside the repo** at `./.dotnet`.
- Always: `source scripts/dev-env.sh` before any `dotnet` command.
- Builds from several agents share `obj/`/`bin/`: **always** build through
  `scripts/build.sh <project> [args]` (it serializes builds with a lock).
  Example: `scripts/build.sh src/BandcampDownloader.Avalonia/BandcampDownloader.Avalonia.csproj -v q`
- Do not add/remove NuGet packages or edit `.csproj` files: ask the Lead.
- Avalonia 11.3.22 is used (not 12.x).

### C# namespace gotcha

The Avalonia project lives in namespace `BandcampDownloader.Avalonia`, so **inline**
`Avalonia.Controls.X` references inside that namespace resolve wrongly (`Avalonia` matches the
current namespace). Use `using Avalonia.Controls;` at the top of the file (using directives resolve
globally) and short type names — never fully-qualified `Avalonia.*` names in code.

```csharp
// Good
using Avalonia.Controls;
using Avalonia.Interactivity;
internal sealed partial class Foo : UserControl { private void OnClick(object? s, RoutedEventArgs e) { } }

// Bad (CS0234 in this project)
internal sealed partial class Foo : Avalonia.Controls.UserControl { }
```

## Localization

The localized strings live in `src/BandcampDownloader.Core/Properties/Resources.*.resx` (130 keys,
22 languages). In XAML use the `loc:Loc` markup extension with an **explicit** key:

```xml
xmlns:loc="clr-namespace:BandcampDownloader.Avalonia.Core.Localization"
<Button Content="{loc:Loc buttonStart}" />
<TextBox ToolTip.Tip="{loc:Loc textBoxDownloadsPath_ToolTip}" />
```

Key mapping rule for porting WPF `{lex:Loc}` (no explicit key): the key is the camelCase of the WPF
element `x:Name`, except:

| WPF property | Key |
|---|---|
| `Content` / `Text` | `camelCase(x:Name)` (e.g. `ButtonStart` → `buttonStart`) |
| `ToolTip` | `camelCase(x:Name) + "_ToolTip"` (e.g. `TextBoxDownloadsPath` → `textBoxDownloadsPath_ToolTip`) |
| `Window.Title` | `camelCase(x:Name) + "_Title"` (e.g. `windowSettings` → `windowSettings_Title`) |

WPF `{lex:Loc someKey}` explicit keys are used as-is (`{loc:Loc someKey}`).

**Mnemonics:** the resx values come from the WPF application, where `_` marks an Alt-key mnemonic
(`"_Download"`). `Localizer.GetString` strips them automatically for every localized string (`"__"` →
literal `_`), so never pre-strip keys/values in XAML or code. The WPF head still uses its own
`WPFLocalizeExtension` and is unaffected.

From code, use `Localizer.Instance["key"]` (see `Localizer.GetString`; missing keys are rendered as
the key itself and logged). Language changes refresh all `{loc:Loc}` bindings automatically
(`ILanguageService.CultureChanged`).

For composed strings (plain string + value, e.g. current version), build the text in code-behind:
`$"{Localizer.Instance["labelCurrentVersion"]} {Constants.APP_VERSION_FORMATTED}"`.

## Localized enum ComboBoxes

`EnumOption<T>` wraps enum values with their localized description. The data template
`EnumOptionTemplate` is defined in `App.axaml` resources:

```xml
<ComboBox x:Name="ComboBoxTheme" ItemTemplate="{StaticResource EnumOptionTemplate}" />
```

```csharp
// load
ComboBoxTheme.ItemsSource = EnumOption<Skin>.All();
ComboBoxTheme.SelectedItem = EnumOption<Skin>.FromValue(userSettings.Theme);
// save
if (ComboBoxTheme.SelectedItem is EnumOption<Skin> option) userSettings.Theme = option.Value;
```

This reproduces the WPF `lex:EnumComboBox` + `Description` attribute/resource behaviour
(resource key `EnumType_Value` first, then `[Description]`, then the enum name).

## Settings tabs pattern

Each `UserControlSettingsX` implements `IUserControlSettings`:
`void CancelChanges()`, `void LoadSettings(IUserSettings)`, `bool SaveSettings()`.

- `DataContext = userSettings` (set in ctor after `InitializeComponent`).
- Display bindings **OneWay only** (Config.Net does not raise change notifications; the WPF version
  used `UpdateSourceTrigger=Explicit` and saved manually). Example:
  `<CheckBox IsChecked="{Binding CheckBoxModifyTagsProperty...}"` → use `IsChecked="{Binding ModifyTags, Mode=OneWay}"`.
- `LoadSettings` re-assigns `DataContext` and re-populates ComboBoxes/controls explicitly.
- `SaveSettings()` reads the control values and writes them to the settings. **Validate**: if a
  numeric/format value is invalid, show a message box, keep the window open and return `false`
  (`SettingsWindow` stays open when any tab returns false). Parsing uses
  `CultureInfo.InvariantCulture` (see the WPF `DoubleRule` / `Mp3ExtensionRule`).
- Controls must keep the WPF `x:Name`s where possible (`CheckBoxModifyTags`, `TextBoxFileNameFormat`,
  ...). The `SettingsWindow` shell and stubs are already in place; replace stub content in place.

## Common snippets

```csharp
// Folder picker (replaces WPF OpenFolderDialog)
var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
{
    Title = Localizer.Instance["labelDownloadsPath"],
    AllowMultiple = false,
});
if (folders.Count > 0)
{
    var path = folders[0].TryGetLocalPath();
    if (path != null) TextBoxDownloadsPath.Text = Path.Combine(path, "{artist}", "{album}");
}

// Message box (returns true for OK/Yes)
var confirmed = await MessageBox.ShowAsync(this, message, MessageBoxButtons.OkCancel,
    MessageBoxIcon.Question, "Bandcamp Downloader");

// UI thread
await UiDispatcher.ExecuteOnUiAsync(() => LabelProgress.Content = text);

// Open a URL in the browser
Hyperlink.OpenUrl(Constants.URL_RELEASES); // Core Helpers.UrlHelper

// Clickable link-styled TextBlock
Hyperlink.MakeLookLikeLink(TextBlockReleases);
TextBlockReleases.PointerPressed += (_, _) => Hyperlink.OpenUrl(Constants.URL_RELEASES);

// macOS system sound (replaces WPF SoundPlayer)
if (OperatingSystem.IsMacOS())
    Process.Start("afplay", "/System/Library/Sounds/Glass.aiff");
else
    Process.Start(new ProcessStartInfo("afplay") ...); // keep it optional / guarded

// Icons / images
Icon = AppAssets.WindowIcon;                                  // code
<Image Source="avares://BandcampDownloader.Avalonia/Assets/Tags.png" />   // XAML
```

Core services (all registered in DI): `ISettingsService`, `IDownloadManager`,
`IUpdatesService`, `IHttpService`, `ILanguageService`, `ILoggingService`,
`IExceptionHandler`, `IUserNotifier`, plus the Avalonia `IThemeService`
(`BandcampDownloader.Avalonia.Core.Theme`). Resolve with
`DependencyInjectionHelper.GetService<T>()` (the static container) or constructor injection for
`MainWindow`.

## Avalonia 11 notes used by this port

- Window closing: `Closing` event with `WindowClosingEventArgs`; cancel via `e.Cancel = true`.
- `Window.Closing`, `Opening`, `Opened`; use `Opened` where WPF used `Loaded`.
- No `RichTextBox`: the log uses a `ListBox` of entries (`ItemsControl` + `ScrollViewer` also works);
  colors via `IBrush`/`SolidColorBrush` (`Brushes.*`).
- `TextBox.Watermark` for placeholder text.
- `SelectableTextBlock` for selectable read-only text (changelog, log fallback).
- Grid shorthand `RowDefinitions="Auto,*"` and `StackPanel.Spacing` are supported.
- `Button.IsDefault` / `IsCancel` are supported.
- `ToolTip.Tip` is the attached-property syntax for tooltips.
- `ItemsControl`/`ListBox` `ItemTemplate` + `DataTemplate`; no `DisplayMemberPath` — use
  `ItemTemplate="{StaticResource EnumOptionTemplate}"` (or a `DataTemplate`).

## Verification

- Build: `scripts/build.sh src/BandcampDownloader.Avalonia/BandcampDownloader.Avalonia.csproj -v q`
  must be green (warnings are errors: `TreatWarningsAsErrors`).
- The app can be launched for a smoke test (settings/logs go to a temp dir to avoid touching the
  real profile):

```bash
source scripts/dev-env.sh
mkdir -p .appdata-run
BANDCAMPDOWNLOADER_DATA_DIR="$PWD/.appdata-run" \
  src/BandcampDownloader.Avalonia/bin/Debug/BandcampDownloader.Avalonia
```

- Headless UI rendering (frame → PNG, no display required): the validated recipe is in
  `docs/macos-avalonia-spike.md`; the headless UI tests use it from
  `src/BandcampDownloader.Avalonia.Tests` (see that project for the `[AvaloniaTest]` setup).

## Packaging

- `scripts/package-macos.sh` builds `dist/Bandcamp Downloader.app` plus
  `dist/BandcampDownloader-<version>-macOS-<arch>.zip` (self-contained, ad-hoc signed). It reads the
  version from `src/Directory.Build.props`, takes the shared `.msbuild-lock.d` lock around
  `dotnet publish`, and works both locally (`.dotnet` SDK) and on CI (system `dotnet`).
  `--launch` verifies a packaged build: LaunchServices launch (5s liveness) + a direct launch with
  `BANDCAMPDOWNLOADER_DATA_DIR` to check that settings/log are written.
- `.github/workflows/macos.yml` runs build/test for both mac heads, then packages and uploads
  `dist/*.zip` as the **BandcampDownloader-macOS** artifact.
- The macOS menu bar name comes from `Application.Name = "Bandcamp Downloader"` in
  `App.Initialize()` (CFBundleName in `Info.plist` alone is not enough — verified). See
  `packaging/macos/README.md` for the bundle layout, Info.plist template and signing caveats.
