using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BandcampDownloader.Avalonia.Core;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Core;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Core.Updates;
using NLog;

namespace BandcampDownloader.Avalonia.UI.Update;

/// <summary>
/// Window displaying the changelog of the latest version, with a shortcut to the GitHub releases page.
/// </summary>
internal sealed partial class WindowUpdate : Window
{
    private readonly IUpdatesService _updatesService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public WindowUpdate()
    {
        _updatesService = DependencyInjectionHelper.GetService<IUpdatesService>();

        InitializeComponent();
        Icon = AppAssets.WindowIcon;

        // No Markdown viewer on Avalonia: the releases page is only reachable through this link.
        Hyperlink.MakeLookLikeLink(TextBlockReleasesWebsite);
    }

    private void ButtonDownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        // The Windows version downloaded and extracted a .zip archive; on macOS the application is not
        // distributed this way, so we simply send the user to the releases page.
        Hyperlink.OpenUrl(Constants.URL_RELEASES);
    }

    private void TextBlockReleasesWebsite_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        Hyperlink.OpenUrl(Constants.URL_RELEASES);
        e.Handled = true;
    }

    private async void WindowUpdate_Opened(object sender, EventArgs e)
    {
        Version latestVersion;
        try
        {
            latestVersion = await _updatesService.GetLatestVersionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get latest version");

            // Do nothing, the button will stay disabled
            return;
        }

        if (latestVersion.IsNewerVersion())
        {
            await UiDispatcher.ExecuteOnUiAsync(() => ButtonDownloadUpdate.IsEnabled = true).ConfigureAwait(false);
        }
    }

    private void WindowUpdate_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
