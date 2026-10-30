using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using BandcampDownloader.Avalonia.Core;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Avalonia.UI.Settings;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Avalonia.UI.Update;
using BandcampDownloader.Bandcamp.Download;
using BandcampDownloader.Core.Logging;
using BandcampDownloader.Core.Updates;
using BandcampDownloader.Settings;
using NLog;

namespace BandcampDownloader.Avalonia;

/// <summary>
/// Main window of the application: lets the user enter the URLs to download and displays the
/// downloads progress and log.
/// </summary>
internal sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<LogEntry> _logEntries = [];
    private readonly IUserSettings _userSettings;
    private readonly IDownloadManager _downloadManager;
    private readonly IUpdatesService _updatesService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private CancellationTokenSource _downloadCts;

    /// <summary>
    /// True if there are active downloads; false otherwise.
    /// </summary>
    private bool _activeDownloads;

    /// <summary>
    /// True once the user confirmed that the window can be closed during active downloads.
    /// </summary>
    private bool _closeConfirmed;

    /// <summary>
    /// Used to compute and display the download speed.
    /// </summary>
    private DateTime _lastDownloadSpeedUpdate;

    /// <summary>
    /// Used to compute and display the download speed.
    /// </summary>
    private long _lastTotalReceivedBytes;

    public MainWindow(ISettingsService settingsService, IDownloadManager downloadManager, IUpdatesService updatesService)
    {
        _userSettings = settingsService.GetUserSettings();
        _downloadManager = downloadManager;
        _updatesService = updatesService;

        _downloadManager.DownloadProgressChanged += DownloadProgressChanged;

        InitializeComponent();

        DataContext = _userSettings;
        Icon = AppAssets.WindowIcon;

        ItemsControlLog.ItemsSource = _logEntries;
        LabelNewVersion.Foreground = _userSettings.Theme == Skin.Dark ? Brushes.Orange : Brushes.Red;

        UpdateUrlsPlaceholderVisibility();
    }

    private void TextBoxUrls_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateUrlsPlaceholderVisibility();
    }

    private void UpdateUrlsPlaceholderVisibility()
    {
        TextBoxUrlsPlaceholder.IsVisible = string.IsNullOrEmpty(TextBoxUrls.Text);
    }

    private async void ButtonBrowse_Click(object sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = Localizer.Instance["labelDownloadsPath"],
                AllowMultiple = false,
            });

        if (folders.Count == 0)
        {
            return;
        }

        var folderPath = folders[0].TryGetLocalPath();
        if (folderPath == null)
        {
            return;
        }

        // Keep the placeholders so that each album is stored in its own folder.
        var downloadsPath = Path.Combine(folderPath, "{artist}", "{album}");

        // The settings are not updated by the binding unless the text box loses the focus: save them
        // explicitly so that the folder picker choice is taken into account by the downloads.
        _userSettings.DownloadsPath = downloadsPath;
        TextBoxDownloadsPath.Text = downloadsPath;
    }

    private async void ButtonOpenSettingsWindow_Click(object sender, RoutedEventArgs e)
    {
        var windowSettings = new SettingsWindow(_activeDownloads);
        await windowSettings.ShowDialog(this);
    }

    private void CheckBoxDownloadDiscography_Changed(object sender, RoutedEventArgs e)
    {
        // The check box is only bound OneWay (the settings do not raise change notifications): save the
        // new value explicitly.
        _userSettings.DownloadArtistDiscography = CheckBoxDownloadDiscography.IsChecked == true;
    }

    private async void ButtonStart_Click(object sender, RoutedEventArgs e)
    {
        var inputUrls = TextBoxUrls.Text;

        await Task.Run(
            async () =>
            {
                _downloadCts = new CancellationTokenSource();

                if (string.IsNullOrWhiteSpace(inputUrls))
                {
                    // No URL to look
                    await LogAsync("Paste some albums URLs to be downloaded", DownloadProgressChangedLevel.Error).ConfigureAwait(false);
                    return;
                }

                // Set controls to "downloading..." state
                _activeDownloads = true;
                await UpdateControlsStateAsync(true).ConfigureAwait(false);

                _logger.Info($"Starting download with the following input URLs:{Environment.NewLine}{inputUrls}");
                await LogAsync("Starting download...", DownloadProgressChangedLevel.Info).ConfigureAwait(false);

                try
                {
                    await StartDownloadAsync(inputUrls, _downloadCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await LogAsync("Downloads cancelled by user", DownloadProgressChangedLevel.Info).ConfigureAwait(false);
                }

                // Reset controls to "ready" state
                _activeDownloads = false;
                _lastTotalReceivedBytes = 0;
                await UpdateControlsStateAsync(false).ConfigureAwait(false);
                await ModifyMouseCursorAsync(null).ConfigureAwait(false);

                if (_userSettings.EnableApplicationSounds)
                {
                    // Play the same kind of "download finished" sound as the Windows version
                    try
                    {
                        PlayFinishedSound();
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                        await LogAsync("Could not play 'finished' sound", DownloadProgressChangedLevel.Error).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
    }

    private async void ButtonStop_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = await MessageBox.ShowAsync(
            this,
            Localizer.Instance["messageBoxCancelDownloadsText"],
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (!confirmed || !_activeDownloads)
        {
            // If the user cancelled the cancellation or if downloads finished while he chose to cancel
            return;
        }

        await ModifyMouseCursorAsync(StandardCursorType.Wait).ConfigureAwait(false);
        ButtonStop.IsEnabled = false;

        await _downloadCts.CancelAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Plays a "download finished" sound with the macOS built-in <c>afplay</c> command.
    /// </summary>
    private static void PlayFinishedSound()
    {
        if (OperatingSystem.IsMacOS())
        {
            Process.Start("afplay", "/System/Library/Sounds/Glass.aiff");
        }
    }

    private async Task ModifyMouseCursorAsync(StandardCursorType? cursorType)
    {
        await UiDispatcher.ExecuteOnUiAsync(
            () =>
            {
                Cursor = cursorType.HasValue ? new Cursor(cursorType.Value) : null;
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Displays a message if a new version is available.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        Version latestVersion;
        try
        {
            latestVersion = await _updatesService.GetLatestVersionAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get latest version");

            await ShowNewVersionLabelAsync(Localizer.Instance["labelVersionError"]).ConfigureAwait(false);
            return;
        }

        if (latestVersion.IsNewerVersion())
        {
            await ShowNewVersionLabelAsync(Localizer.Instance["labelVersionNewUpdateAvailable"]).ConfigureAwait(false);
        }
    }

    private async Task ShowNewVersionLabelAsync(string content)
    {
        await UiDispatcher.ExecuteOnUiAsync(
            () =>
            {
                LabelNewVersion.Text = content;
                LabelNewVersion.IsVisible = true;
            }).ConfigureAwait(false);
    }

    private async void DownloadProgressChanged(object sender, DownloadProgressChangedArgs eventArgs)
    {
        await LogAsync(eventArgs.Message, eventArgs.Level).ConfigureAwait(false);
    }

    private void LabelNewVersion_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        var windowUpdate = new WindowUpdate
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        windowUpdate.Show(this);
        e.Handled = true;
    }

    /// <summary>
    /// Logs to file and displays the specified message in the log.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="downloadProgressChangedLevel">The log level.</param>
    private async Task LogAsync(string message, DownloadProgressChangedLevel downloadProgressChangedLevel)
    {
        // Log to file
        _logger.Log(downloadProgressChangedLevel.ToNLogLevel(), message);

        // Log to window
        if (_userSettings.ShowVerboseLog || downloadProgressChangedLevel == DownloadProgressChangedLevel.Error || downloadProgressChangedLevel == DownloadProgressChangedLevel.Info || downloadProgressChangedLevel == DownloadProgressChangedLevel.IntermediateSuccess || downloadProgressChangedLevel == DownloadProgressChangedLevel.Success)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss") + " ";

            await UiDispatcher.ExecuteOnUiAsync(
                () =>
                {
                    // Only follow the log automatically if the user was already at the bottom of it
                    var isScrolledToEnd = ScrollViewerLog.Offset.Y + ScrollViewerLog.Viewport.Height >= ScrollViewerLog.Extent.Height - 2;

                    _logEntries.Add(new LogEntry(timestamp, message, GetLogColor(downloadProgressChangedLevel)));

                    if (isScrolledToEnd)
                    {
                        // The scroll extent is only updated after the layout pass: scroll at the end of
                        // the current UI thread queue.
                        Dispatcher.UIThread.Post(() => ScrollViewerLog.ScrollToEnd(), DispatcherPriority.Background);
                    }
                }).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns the color associated to the specified <see cref="DownloadProgressChangedLevel" />, as in
    /// the Windows version (the colors depend on the current theme).
    /// </summary>
    private IBrush GetLogColor(DownloadProgressChangedLevel downloadProgressChangedLevel)
    {
        var isDarkTheme = _userSettings.Theme == Skin.Dark;

        return downloadProgressChangedLevel switch
        {
            DownloadProgressChangedLevel.Info => isDarkTheme ? new SolidColorBrush(Color.Parse("#f0f0f0")) : Brushes.Black,
            DownloadProgressChangedLevel.VerboseInfo => isDarkTheme ? Brushes.LightSkyBlue : Brushes.MediumBlue,
            DownloadProgressChangedLevel.IntermediateSuccess => isDarkTheme ? Brushes.LightSkyBlue : Brushes.MediumBlue,
            DownloadProgressChangedLevel.Success => isDarkTheme ? Brushes.Lime : Brushes.Green,
            DownloadProgressChangedLevel.Warning => isDarkTheme ? Brushes.Orange : Brushes.OrangeRed,
            DownloadProgressChangedLevel.Error => Brushes.Red,
            _ => Brushes.Red,
        };
    }

    /// <summary>
    /// Starts downloads.
    /// </summary>
    private async Task StartDownloadAsync(string inputUrls, CancellationToken cancellationToken)
    {
        await _downloadManager.InitializeAsync(inputUrls, cancellationToken).ConfigureAwait(false);

        // Set progressBar max value
        var maxProgressBarValue = _userSettings.RetrieveFilesSize ? _downloadManager.GetTotalBytesToDownload() : _downloadManager.GetTotalFilesCountToDownload();

        if (maxProgressBarValue > 0)
        {
            await UiDispatcher.ExecuteOnUiAsync(
                () =>
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Maximum = maxProgressBarValue;
                }).ConfigureAwait(false);
        }

        var timersCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start timer to update progress on UI
        var updateProgressTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        // In fire and forget
        _ = Task.Run(
            async () =>
            {
                try
                {
                    while (await updateProgressTimer.WaitForNextTickAsync(timersCts.Token).ConfigureAwait(false))
                    {
                        await UpdateProgressAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when the downloads stop
                }
            },
            timersCts.Token);

        // Start timer to update download speed on UI
        var updateDownloadSpeedTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
        // In fire and forget
        _ = Task.Run(
            async () =>
            {
                try
                {
                    while (await updateDownloadSpeedTimer.WaitForNextTickAsync(timersCts.Token).ConfigureAwait(false))
                    {
                        await UpdateDownloadSpeedAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when the downloads stop
                }
            },
            timersCts.Token);

        // Start downloading albums
        // ReSharper disable once PossiblyMistakenUseOfCancellationToken : this is the correct token
        await _downloadManager.StartDownloadsAsync(cancellationToken).ConfigureAwait(false);

        // Stop timers
        await timersCts.CancelAsync().ConfigureAwait(false);

        // Update progress one last time to make sure the downloaded bytes displayed on UI is up-to-date
        await UpdateProgressAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the state of the controls.
    /// </summary>
    /// <param name="downloadStarted">True if the download just started; false if it just stopped.</param>
    private async Task UpdateControlsStateAsync(bool downloadStarted)
    {
        await UiDispatcher.ExecuteOnUiAsync(
            () =>
            {
                if (downloadStarted)
                {
                    // We just started the download
                    ButtonBrowse.IsEnabled = false;
                    ButtonStart.IsEnabled = false;
                    ButtonStop.IsEnabled = true;
                    CheckBoxDownloadDiscography.IsEnabled = false;
                    LabelProgress.Text = "";
                    ProgressBar.IsIndeterminate = true;
                    ProgressBar.Value = ProgressBar.Minimum;
                    _logEntries.Clear();
                    ScrollViewerLog.ScrollToHome();
                    TextBoxDownloadsPath.IsReadOnly = true;
                    TextBoxUrls.IsReadOnly = true;
                }
                else
                {
                    // We just finished the download (or user has cancelled)
                    ButtonBrowse.IsEnabled = true;
                    ButtonStart.IsEnabled = true;
                    ButtonStop.IsEnabled = false;
                    CheckBoxDownloadDiscography.IsEnabled = true;
                    LabelDownloadSpeed.Text = "";
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = ProgressBar.Minimum;
                    TextBoxDownloadsPath.IsReadOnly = false;
                    TextBoxUrls.IsReadOnly = false;
                }
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the download speed on UI.
    /// </summary>
    private async Task UpdateDownloadSpeedAsync()
    {
        var now = DateTime.Now;

        // Compute new progress values
        var totalReceivedBytes = _downloadManager.GetTotalBytesReceived();

        var bytesPerSecond = (totalReceivedBytes - _lastTotalReceivedBytes) /
                             (now - _lastDownloadSpeedUpdate).TotalSeconds;
        _lastTotalReceivedBytes = totalReceivedBytes;
        _lastDownloadSpeedUpdate = now;

        var downloadSpeedLabelContent = (bytesPerSecond / 1024).ToString("0.0") + " kB/s";

        await UiDispatcher.ExecuteOnUiAsync(() => LabelDownloadSpeed.Text = downloadSpeedLabelContent).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the progress label on UI.
    /// </summary>
    private async Task UpdateProgressAsync()
    {
        // Compute new progress values
        var totalReceivedBytes = _downloadManager.GetTotalBytesReceived();
        var bytesToDownload = _downloadManager.GetTotalBytesToDownload();

        // Compute progress
        var labelProgressContent = (totalReceivedBytes / (1024 * 1024)).ToString("0.00") + " MB" +
                                   (_userSettings.RetrieveFilesSize ? " / " + (bytesToDownload / (1024 * 1024)).ToString("0.00") + " MB" : "");

        double progressBarValue;
        if (_userSettings.RetrieveFilesSize)
        {
            // Update progress bar based on bytes received
            progressBarValue = totalReceivedBytes;
        }
        else
        {
            // Update progress bar based on downloaded files
            progressBarValue = _downloadManager.GetTotalFilesCountReceived();
        }

        await UiDispatcher.ExecuteOnUiAsync(
            () =>
            {
                LabelProgress.Text = labelProgressContent;
                ProgressBar.Value = progressBarValue;
            }).ConfigureAwait(false);
    }

    private void WindowMain_Closing(object sender, WindowClosingEventArgs e)
    {
        if (!_activeDownloads || _closeConfirmed)
        {
            return;
        }

        // There are active downloads: ask for confirmation asynchronously, and cancel this first
        // closing attempt. The window is closed explicitly if the user confirms.
        e.Cancel = true;
        _ = ConfirmCloseWhenDownloadingAsync();
    }

    private async Task ConfirmCloseWhenDownloadingAsync()
    {
        try
        {
            var messageBox = new MessageBoxWindow();
            messageBox.Configure(
                Localizer.Instance["messageBoxCloseWindowWhenDownloadingText"],
                MessageBoxButtons.OkCancel,
                MessageBoxIcon.Warning,
                "Bandcamp Downloader");

            // The Windows version uses a specific label for the "close application" button.
            var buttonOk = messageBox.FindControl<Button>("ButtonOk");
            if (buttonOk != null)
            {
                buttonOk.Content = Localizer.Instance["messageBoxCloseWindowWhenDownloadingButtonOk"];
            }

            var confirmed = await messageBox.ShowDialogAsync(this).ConfigureAwait(true);
            if (!confirmed)
            {
                return;
            }

            _closeConfirmed = true;
            Close();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Could not confirm the window closing");
        }
    }

    private void WindowMain_Opened(object sender, EventArgs e)
    {
        if (!_userSettings.CheckForUpdates)
        {
            return;
        }

        // In fire and forget in order to prevent delaying the window access
        _ = CheckForUpdatesAsync();
    }

    /// <summary>
    /// One entry of the log displayed in the main window.
    /// </summary>
    private sealed class LogEntry
    {
        public LogEntry(string timestamp, string message, IBrush foreground)
        {
            Timestamp = timestamp;
            Message = message;
            Foreground = foreground;
        }

        public string Timestamp { get; }

        public string Message { get; }

        public IBrush Foreground { get; }
    }
}
