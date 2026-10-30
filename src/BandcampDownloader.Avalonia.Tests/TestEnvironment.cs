using System;
using System.IO;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Avalonia.Core.Theme;
using BandcampDownloader.Avalonia.Platform;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Core.Localization;
using BandcampDownloader.Core.Logging;
using BandcampDownloader.Core.Notifications;
using BandcampDownloader.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace BandcampDownloader.Avalonia.Tests;

/// <summary>
/// Reproduces the composition of <c>App.OnFrameworkInitializationCompleted</c> for the headless tests:
/// the Avalonia lifetime is not a desktop lifetime in the headless platform, so the application does
/// not initialize the dependency injection container by itself.
/// </summary>
/// <remarks>
/// <para>
/// <c>AppPaths</c> resolves the settings/log paths from the <c>BANDCAMPDOWNLOADER_DATA_DIR</c>
/// environment variable, which must be set <b>before</b> its static constructor runs (first member
/// access). This class is an assembly-level set-up fixture, so it runs before every test.
/// </para>
/// <para>
/// The container is the real one (same services as the application, same Avalonia platform services),
/// so the windows are constructed exactly like in the application.
/// </para>
/// </remarks>
[SetUpFixture]
public sealed class TestEnvironment
{
    private static readonly object InitLock = new();
    private static bool _initialized;
    private static string _dataDirectory = null!;
    private static IContainer _container = null!;
    private static IUserSettings _userSettings = null!;

    /// <summary>
    /// The temporary directory used as the application data directory by the tests.
    /// </summary>
    public static string DataDirectory
    {
        get
        {
            EnsureInitialized();
            return _dataDirectory;
        }
    }

    /// <summary>
    /// The dependency injection container of the tests (same composition as the real application).
    /// </summary>
    internal static IContainer Container
    {
        get
        {
            EnsureInitialized();
            return _container;
        }
    }

    [OneTimeSetUp]
    public void SetUp()
    {
        EnsureInitialized();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        TryDeleteDataDirectory();
    }

    /// <summary>
    /// Returns the user settings of the dependency injection container, initialized with a temporary
    /// application data directory.
    /// </summary>
    public static IUserSettings GetUserSettings()
    {
        EnsureInitialized();
        return _userSettings;
    }

    /// <summary>
    /// Creates a new empty directory inside <see cref="DataDirectory" /> and returns its full path.
    /// </summary>
    public static string CreateTempDirectory(string name)
    {
        var path = Path.Combine(DataDirectory, name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (_initialized)
            {
                return;
            }

            // Must be done before anything reads AppPaths (see the remarks on this class).
            _dataDirectory = Path.Combine(Path.GetTempPath(), "BandcampDownloader.Avalonia.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dataDirectory);
            Environment.SetEnvironmentVariable("BANDCAMPDOWNLOADER_DATA_DIR", _dataDirectory);

            // Same composition as App.OnFrameworkInitializationCompleted (minus the window open).
            _container = DependencyInjectionHelper.InitializeContainer(ConfigureAvaloniaServices);

            var loggingService = _container.GetService<ILoggingService>();
            loggingService.InitializeLogger();

            var settingsService = _container.GetService<ISettingsService>();
            _userSettings = settingsService.InitializeSettings();

            var languageService = _container.GetService<ILanguageService>();
            Localizer.Instance.Initialize(languageService);
            languageService.ApplyLanguage(_userSettings.Language);

            // The UI tests must not hit the network: the update check is triggered when MainWindow opens.
            _userSettings.CheckForUpdates = false;

            _initialized = true;
        }
    }

    private static void ConfigureAvaloniaServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IUserNotifier, AvaloniaUserNotifier>();
        serviceCollection.AddSingleton<IThemeService, AvaloniaThemeService>();
    }

    private static void TryDeleteDataDirectory()
    {
        if (string.IsNullOrEmpty(_dataDirectory))
        {
            return;
        }

        try
        {
            Environment.SetEnvironmentVariable("BANDCAMPDOWNLOADER_DATA_DIR", null);
            Directory.Delete(_dataDirectory, true);
        }
        catch (Exception)
        {
            // Best effort only: a leaked file in /tmp should not fail the test run.
        }
    }
}
