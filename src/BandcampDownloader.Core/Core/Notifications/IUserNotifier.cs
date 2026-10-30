namespace BandcampDownloader.Core.Notifications;

/// <summary>
/// Platform-independent way for the core library to report an error to the user.
/// Implemented by each UI head (WPF message box, Avalonia message box, ...).
/// </summary>
internal interface IUserNotifier
{
    /// <summary>
    /// Displays the specified error message to the user.
    /// </summary>
    void ShowError(string message, string title = "Bandcamp Downloader");
}
