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

internal sealed partial class UserControlSettingsAdvanced : UserControl, IUserControlSettings
{
    private readonly ISettingsService _settingsService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public UserControlSettingsAdvanced()
    {
        _settingsService = DependencyInjectionHelper.GetService<ISettingsService>();

        InitializeComponent();

        // The warning is only displayed when the settings are opened during downloads.
        Loaded += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is SettingsWindow settingsWindow)
            {
                DockPanelInfo.IsVisible = settingsWindow.ActiveDownloads;
            }
        };

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

        // AllowedFileSizeDifference is stored as a ratio but displayed as a percentage.
        TextBoxAllowedFileSizeDifference.Text = (userSettings.AllowedFileSizeDifference * 100).ToString(CultureInfo.InvariantCulture);
        TextBoxDownloadMaxTries.Text = userSettings.DownloadMaxTries.ToString(CultureInfo.InvariantCulture);
        TextBoxDownloadRetryCooldown.Text = userSettings.DownloadRetryCooldown.ToString(CultureInfo.InvariantCulture);
        TextBoxDownloadRetryExponent.Text = userSettings.DownloadRetryExponent.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Saves settings to the user settings. Returns false if the user input is invalid.
    /// </summary>
    public bool SaveSettings()
    {
        var userSettings = _settingsService.GetUserSettings();
        var errors = new List<string>();

        // AllowedFileSizeDifference is displayed as a percentage but stored as a ratio.
        if (double.TryParse(TextBoxAllowedFileSizeDifference.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var allowedFileSizeDifference))
        {
            userSettings.AllowedFileSizeDifference = allowedFileSizeDifference / 100;
        }
        else
        {
            errors.Add($"{Localizer.Instance["labelAllowedFileSizeDifference"]}: not a numeric value");
        }

        if (int.TryParse(TextBoxDownloadMaxTries.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var downloadMaxTries))
        {
            userSettings.DownloadMaxTries = downloadMaxTries;
        }
        else
        {
            errors.Add($"{Localizer.Instance["labelDownloadMaxTries"]}: not a numeric value");
        }

        if (double.TryParse(TextBoxDownloadRetryCooldown.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var downloadRetryCooldown))
        {
            userSettings.DownloadRetryCooldown = downloadRetryCooldown;
        }
        else
        {
            errors.Add($"{Localizer.Instance["labelDownloadRetryCooldown"]}: not a numeric value");
        }

        if (double.TryParse(TextBoxDownloadRetryExponent.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var downloadRetryExponent))
        {
            userSettings.DownloadRetryExponent = downloadRetryExponent;
        }
        else
        {
            errors.Add($"{Localizer.Instance["labelDownloadRetryExponent"]}: not a numeric value");
        }

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
