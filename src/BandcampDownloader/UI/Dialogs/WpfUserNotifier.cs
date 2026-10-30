using System;
using System.Windows;
using BandcampDownloader.Core.Notifications;
using WpfMessageBoxLibrary;

namespace BandcampDownloader.UI.Dialogs;

/// <summary>
/// WPF implementation of <see cref="IUserNotifier" />.
/// </summary>
internal sealed class WpfUserNotifier : IUserNotifier
{
    public void ShowError(string message, string title = "Bandcamp Downloader")
    {
        var application = Application.Current;
        if (application == null)
        {
            return;
        }

        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => ShowError(message, title));
            return;
        }

        var msgProperties = new WpfMessageBoxProperties
        {
            Button = WpfMessageBoxButton.OK,
            ButtonOkText = Properties.Resources.messageBoxButtonOK,
            Image = WpfMessageBoxImage.Error,
            Text = message,
            Title = title,
        };

        WpfMessageBox.Show(application.MainWindow, ref msgProperties);
    }
}
