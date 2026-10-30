using System;
using System.Globalization;
using Avalonia.Controls;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Settings;
using NLog;

namespace BandcampDownloader.Avalonia.UI.Settings;

internal sealed partial class UserControlSettingsNetwork : UserControl, IUserControlSettings
{
    private readonly ISettingsService _settingsService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public UserControlSettingsNetwork()
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

        RadioButtonNoProxy.IsChecked = userSettings.Proxy == ProxyType.None;
        RadioButtonSystemProxy.IsChecked = userSettings.Proxy == ProxyType.System;
        RadioButtonManualProxy.IsChecked = userSettings.Proxy == ProxyType.Manual;

        TextBoxHttpAddress.Text = userSettings.ProxyHttpAddress;
        TextBoxHttpPort.Text = userSettings.ProxyHttpPort.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Saves settings to the user settings. Returns false if the user input is invalid.
    /// </summary>
    public bool SaveSettings()
    {
        var userSettings = _settingsService.GetUserSettings();

        if (RadioButtonManualProxy.IsChecked == true)
        {
            userSettings.Proxy = ProxyType.Manual;
        }
        else if (RadioButtonSystemProxy.IsChecked == true)
        {
            userSettings.Proxy = ProxyType.System;
        }
        else
        {
            userSettings.Proxy = ProxyType.None;
        }

        userSettings.ProxyHttpAddress = TextBoxHttpAddress.Text ?? string.Empty;

        if (int.TryParse(TextBoxHttpPort.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var proxyHttpPort))
        {
            userSettings.ProxyHttpPort = proxyHttpPort;
        }
        else
        {
            ShowInvalidInputMessage($"{Localizer.Instance["labelHttpPort"]}: not a numeric value");
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
