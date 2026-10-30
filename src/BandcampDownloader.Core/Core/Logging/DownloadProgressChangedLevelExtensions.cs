using System;
using BandcampDownloader.Bandcamp.Download;
using NLog;

namespace BandcampDownloader.Core.Logging;

internal static class DownloadProgressChangedLevelExtensions
{
    /// <summary>
    /// Returns the <see cref="LogLevel" /> associated to the specified <see cref="DownloadProgressChangedLevel" />.
    /// </summary>
    public static LogLevel ToNLogLevel(this DownloadProgressChangedLevel downloadProgressChangedLevel)
    {
        return downloadProgressChangedLevel switch
        {
            DownloadProgressChangedLevel.VerboseInfo => LogLevel.Debug,
            DownloadProgressChangedLevel.Info => LogLevel.Info,
            DownloadProgressChangedLevel.IntermediateSuccess => LogLevel.Info,
            DownloadProgressChangedLevel.Success => LogLevel.Info,
            DownloadProgressChangedLevel.Warning => LogLevel.Warn,
            DownloadProgressChangedLevel.Error => LogLevel.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(downloadProgressChangedLevel), downloadProgressChangedLevel, null),
        };
    }
}
