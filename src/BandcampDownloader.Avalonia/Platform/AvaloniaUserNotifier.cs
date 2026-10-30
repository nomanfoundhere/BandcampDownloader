using System;
using Avalonia.Controls;
using Avalonia.Threading;
using BandcampDownloader.Avalonia.Core;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Core.Notifications;

namespace BandcampDownloader.Avalonia.Platform;

/// <summary>
/// Avalonia implementation of <see cref="IUserNotifier" />.
/// </summary>
internal sealed class AvaloniaUserNotifier : IUserNotifier
{
    public void ShowError(string message, string title = "Bandcamp Downloader")
    {
        void Show()
        {
            var owner = UiDispatcher.MainWindow;
            _ = MessageBox.ShowAsync(owner, message, MessageBoxButtons.Ok, MessageBoxIcon.Error, title);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Show();
        }
        else
        {
            Dispatcher.UIThread.Post(Show);
        }
    }
}
