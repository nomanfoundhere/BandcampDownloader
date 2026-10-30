using System;
using System.Reflection;

namespace BandcampDownloader.Core;

internal static class Constants
{
    /// <summary>
    /// Names of the assemblies that are actual application heads. The version of the entry assembly is
    /// only trusted for those: test hosts (dotnet test, headless runners) also have an entry assembly
    /// (testhost, version 15.0.0.0), which must not be reported as the application version.
    /// </summary>
    private static readonly string[] KnownHeadAssemblyNames = ["BandcampDownloader", "BandcampDownloader.Avalonia"];

    /// <summary>
    /// The version of BandcampDownloader.
    /// </summary>
    public static readonly Version APP_VERSION = GetAppVersion();

    /// <summary>
    /// The formatted version (X.Y.Z) of BandcampDownloader.
    /// </summary>
    public static readonly string APP_VERSION_FORMATTED = APP_VERSION.ToString(3);

    /// <summary>
    /// Returns the application version: the entry assembly version for a real UI head, the Core assembly
    /// version otherwise (so that the version is always available and never null, including in tests).
    /// </summary>
    private static Version GetAppVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var entryAssemblyName = entryAssembly?.GetName();

        if (entryAssemblyName?.Name != null && Array.IndexOf(KnownHeadAssemblyNames, entryAssemblyName.Name) >= 0)
        {
            return entryAssembly.GetName().Version;
        }

        return typeof(Constants).Assembly.GetName().Version ?? new Version(1, 5, 2);
    }

    /// <summary>
    /// The URL redirecting to the help page on translating the app on GitHub.
    /// </summary>
    public const string URL_HELP_TRANSLATE = "https://github.com/Otiel/BandcampDownloader/blob/master/docs/help-translate.md";

    /// <summary>
    /// The URL redirecting to the issues page on GitHub.
    /// </summary>
    public const string URL_ISSUES = "https://github.com/Otiel/BandcampDownloader/issues";

    /// <summary>
    /// The URL redirecting to the latest release on GitHub.
    /// </summary>
    public const string URL_LATEST_RELEASE = "https://github.com/Otiel/BandcampDownloader/releases/latest";

    /// <summary>
    /// The URL redirecting to the releases page on GitHub.
    /// </summary>
    public const string URL_RELEASES = "https://github.com/Otiel/BandcampDownloader/releases";

    /// <summary>
    /// The website URL of BandcampDownloader.
    /// </summary>
    public const string URL_WEBSITE = "https://github.com/Otiel/BandcampDownloader";
}
