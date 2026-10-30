using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BandcampDownloader.Avalonia.Core;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Net;
using NLog;

namespace BandcampDownloader.Avalonia.UI.Update;

/// <summary>
/// Displays the changelog of the application, downloaded from GitHub.
/// </summary>
internal sealed partial class UserControlChangelog : UserControl
{
    private const string CHANGELOG_URL = "https://raw.githubusercontent.com/Otiel/BandcampDownloader/master/CHANGELOG.md";

    private readonly IHttpService _httpService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public UserControlChangelog()
    {
        _httpService = DependencyInjectionHelper.GetService<IHttpService>();

        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async Task<string> DownloadChangelogAsync()
    {
        var httpClient = _httpService.CreateHttpClient();
        var changelog = await httpClient.GetStringAsync(CHANGELOG_URL).ConfigureAwait(false);
        return changelog;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        string changelog;
        try
        {
            changelog = await DownloadChangelogAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex);
            changelog = string.Format(Localizer.Instance["changelogDownloadError"], CHANGELOG_URL);
        }

        await UiDispatcher.ExecuteOnUiAsync(() => TextBlockChangelog.Text = changelog).ConfigureAwait(false);
    }
}
