using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Settings;
using NLog;

namespace BandcampDownloader.Avalonia.UI.Settings;

internal sealed partial class UserControlSettingsDownloads : UserControl, IUserControlSettings
{
    private readonly ISettingsService _settingsService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public UserControlSettingsDownloads()
    {
        _settingsService = DependencyInjectionHelper.GetService<ISettingsService>();

        InitializeComponent();
        LoadSettings(_settingsService.GetUserSettings());
    }

    /// <summary>
    /// Cancels the changes already applied.
    /// </summary>
    public void CancelChanges()
    {
        // Nothing to "unapply"
    }

    /// <summary>
    /// Loads settings from the specified user settings.
    /// </summary>
    public void LoadSettings(IUserSettings userSettings)
    {
        // Re-load the data context in case the settings have changed (Config.Net settings do not
        // raise change notifications, so every control is also populated explicitly below).
        DataContext = userSettings;

        TextBoxMaxConcurrentAlbumsDownloads.Text = userSettings.MaxConcurrentAlbumsDownloads.ToString(CultureInfo.InvariantCulture);
        TextBoxMaxConcurrentTracksDownloads.Text = userSettings.MaxConcurrentTracksDownloads.ToString(CultureInfo.InvariantCulture);
        CheckBoxRetrieveFilesSize.IsChecked = userSettings.RetrieveFilesSize;
    }

    /// <summary>
    /// Saves settings to the user settings. Returns false if the user input is invalid.
    /// </summary>
    public bool SaveSettings()
    {
        var userSettings = _settingsService.GetUserSettings();
        var errors = new List<string>();

        if (int.TryParse(TextBoxMaxConcurrentAlbumsDownloads.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxConcurrentAlbumsDownloads))
        {
            userSettings.MaxConcurrentAlbumsDownloads = maxConcurrentAlbumsDownloads;
        }
        else
        {
            errors.Add($"{Localizer.Instance["labelMaxConcurrentAlbumsDownloads"]}: not a numeric value");
        }

        if (int.TryParse(TextBoxMaxConcurrentTracksDownloads.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxConcurrentTracksDownloads))
        {
            userSettings.MaxConcurrentTracksDownloads = maxConcurrentTracksDownloads;
        }
        else
        {
            errors.Add($"{Localizer.Instance["labelMaxConcurrentTracksDownloads"]}: not a numeric value");
        }

        userSettings.RetrieveFilesSize = CheckBoxRetrieveFilesSize.IsChecked == true;

        if (errors.Count > 0)
        {
            ShowInvalidInputMessage(string.Join(Environment.NewLine, errors));
            return false;
        }

        return true;
    }

    private void ShowInvalidInputMessage(string message)
    {
        _logger.Warn(message);

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            _ = MessageBox.ShowAsync(owner, message, MessageBoxButtons.Ok, MessageBoxIcon.Error, "Bandcamp Downloader");
        }
    }
}
