using System;
using System.IO;

namespace BandcampDownloader.Platform;

/// <summary>
/// Cross-platform paths used by the application (settings file, log file, default download folder).
/// </summary>
/// <remarks>
/// The WPF version used to store its settings and logs next to the executable folder. This is not a
/// sensible location on macOS (and is often read-only on Windows too), so user data now lives in the
/// platform's standard per-user application data directory.
/// </remarks>
internal static class AppPaths
{
    /// <summary>
    /// The user application data directory, for instance
    /// "~/Library/Application Support/BandcampDownloader" on macOS.
    /// </summary>
    public static string AppDataDirectory { get; }

    /// <summary>
    /// The absolute path to the settings file.
    /// </summary>
    public static string SettingsFilePath => Path.Combine(AppDataDirectory, "BandcampDownloader.ini");

    /// <summary>
    /// The absolute path to the log file.
    /// </summary>
    public static string LogFilePath => Path.Combine(AppDataDirectory, "BandcampDownloader.log");

    /// <summary>
    /// The default downloads path format, with its placeholders.
    /// </summary>
    public static string DefaultDownloadsPathFormat { get; }

    /// <summary>
    /// The location where versions up to 1.5.2 (Windows only) stored the settings file: next to the
    /// application folder (more precisely, in the parent of the executable directory).
    /// Null on macOS/Linux, where no legacy location exists.
    /// </summary>
    public static string LegacySettingsFilePath { get; }

    /// <summary>
    /// True if <see cref="AppDataDirectory" /> was overridden with the BANDCAMPDOWNLOADER_DATA_DIR
    /// environment variable (portable/test run). Legacy settings are never imported in that case.
    /// </summary>
    public static bool IsDataDirectoryOverridden { get; }

    static AppPaths()
    {
        IsDataDirectoryOverridden = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BANDCAMPDOWNLOADER_DATA_DIR"));
        AppDataDirectory = GetAppDataDirectory();
        LegacySettingsFilePath = GetLegacySettingsFilePath();

        try
        {
            Directory.CreateDirectory(AppDataDirectory);
        }
        catch (Exception)
        {
            // If the directory cannot be created, the application will still start; writing the
            // settings/log files will then fail and the error will be reported by the caller.
        }

        DefaultDownloadsPathFormat = GetDefaultDownloadsPathFormat();
    }

    private static string GetAppDataDirectory()
    {
        // Escape hatch used by the automated tests and by "portable" runs: store the application data
        // in the specified directory instead of the platform's standard location.
        var overriddenDirectory = Environment.GetEnvironmentVariable("BANDCAMPDOWNLOADER_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(overriddenDirectory))
        {
            return overriddenDirectory;
        }

        if (OperatingSystem.IsMacOS())
        {
            var library = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
            return Path.Combine(library, "BandcampDownloader");
        }

        // Windows: %APPDATA%\BandcampDownloader, Linux: ~/.config/BandcampDownloader
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BandcampDownloader");
    }

    private static string GetDefaultDownloadsPathFormat()
    {
        if (OperatingSystem.IsWindows())
        {
            // Keep the historical Windows default: Desktop\{artist}\{album}
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            return Path.Combine(desktop, "{artist}", "{album}");
        }

        // macOS / Linux: ~/Music/Bandcamp/{artist}/{album}
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        return Path.Combine(music, "Bandcamp", "{artist}", "{album}");
    }

    /// <summary>
    /// Returns the legacy (pre-port) settings file location on Windows, or null on other platforms.
    /// </summary>
    private static string GetLegacySettingsFilePath()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var executableDirectory = AppContext.BaseDirectory;
        var parentDirectory = Directory.GetParent(executableDirectory)?.FullName ?? executableDirectory;
        return Path.Combine(parentDirectory, "BandcampDownloader.ini");
    }
}
