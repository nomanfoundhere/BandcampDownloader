using System;
using System.IO;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Settings;

namespace BandcampDownloader.UnitTests;

/// <summary>
/// Prepares a per-run application data directory for the tests that need the dependency injection
/// container (and therefore <see cref="BandcampDownloader.Platform.AppPaths" />).
/// </summary>
/// <remarks>
/// The application resolves its settings/log paths from <c>AppPaths</c>, whose static constructor is
/// triggered the first time any of its members is read. The only way to redirect it to a temporary
/// directory is the <c>BANDCAMPDOWNLOADER_DATA_DIR</c> environment variable, which must be set
/// <b>before</b> that static constructor runs. This is why the environment variable is set here (in the
/// assembly-level set-up fixture, i.e. before any test fixture runs) and not in the tests themselves.
/// Never touch <c>AppPaths</c> from another type at assembly-load time, or the environment variable may
/// be ignored.
/// </remarks>
[SetUpFixture]
public sealed class TestEnvironment
{
    private static readonly object InitLock = new();
    private static bool _initialized;
    private static string _dataDirectory = null!;
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

    private static void EnsureInitialized()
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
            _dataDirectory = Path.Combine(Path.GetTempPath(), "BandcampDownloader.UnitTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dataDirectory);
            Environment.SetEnvironmentVariable("BANDCAMPDOWNLOADER_DATA_DIR", _dataDirectory);

            var container = DependencyInjectionHelper.InitializeContainer();
            _userSettings = container.GetService<ISettingsService>().InitializeSettings();

            _initialized = true;
        }
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
