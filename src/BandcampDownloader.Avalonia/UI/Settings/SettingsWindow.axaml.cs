using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Settings;
using NLog;

namespace BandcampDownloader.Avalonia.UI.Settings;

internal sealed partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// True if there are active downloads; false otherwise.
    /// </summary>
    public bool ActiveDownloads { get; set; }

    /// <summary>
    /// Creates a new instance of SettingsWindow.
    /// </summary>
    /// <param name="activeDownloads">True if there are active downloads; false otherwise.</param>
    public SettingsWindow(bool activeDownloads)
    {
        _settingsService = DependencyInjectionHelper.GetService<ISettingsService>();

        ActiveDownloads = activeDownloads; // Must be done before UI initialization
        InitializeComponent();

        Icon = AppAssets.WindowIcon;
        PanelSettingsInfo.IsVisible = ActiveDownloads;
        ApplyActiveDownloadsState();
        ShowSection(0);
    }

    private void ListBoxSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowSection(ListBoxSections.SelectedIndex);
    }

    private void ShowSection(int index)
    {
        var sections = new Control[]
        {
            UserControlSettingsGeneral,
            UserControlSettingsTags,
            UserControlSettingsCoverArt,
            UserControlSettingsPlaylist,
            UserControlSettingsDownloads,
            UserControlSettingsNetwork,
            UserControlSettingsAdvanced,
            UserControlSettingsAbout,
        };

        for (var i = 0; i < sections.Length; i++)
        {
            sections[i].IsVisible = i == index;
        }
    }

    private void ApplyActiveDownloadsState()
    {
        UserControlSettingsTags.IsEnabled = !ActiveDownloads;
        UserControlSettingsCoverArt.IsEnabled = !ActiveDownloads;
        UserControlSettingsPlaylist.IsEnabled = !ActiveDownloads;
        UserControlSettingsDownloads.IsEnabled = !ActiveDownloads;
        UserControlSettingsNetwork.IsEnabled = !ActiveDownloads;
        UserControlSettingsAdvanced.IsEnabled = !ActiveDownloads;
        ButtonResetSettings.IsEnabled = !ActiveDownloads;
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        CancelChanges();
        Close();
    }

    private async void ButtonResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var messageBox = new MessageBoxWindow();
        messageBox.Configure(
            Localizer.Instance["messageBoxResetSettingsText"],
            MessageBoxButtons.OkCancel,
            MessageBoxIcon.Question,
            "Bandcamp Downloader");

        // The Windows version uses a specific label for the "reset" confirmation button.
        var buttonOk = messageBox.FindControl<Button>("ButtonOk");
        if (buttonOk != null)
        {
            buttonOk.Content = Localizer.Instance["messageBoxResetSettingsButtonOk"];
        }

        var confirmed = await messageBox.ShowDialogAsync(this);
        if (confirmed)
        {
            ResetSettings();
        }
    }

    private void ButtonSave_Click(object sender, RoutedEventArgs e)
    {
        if (!SaveSettings())
        {
            // Invalid input: keep the window open so that the user can fix it
            return;
        }

        LogSettings();
        Close();
    }

    /// <summary>
    /// Cancels the changes already applied.
    /// </summary>
    private void CancelChanges()
    {
        UserControlSettingsAdvanced.CancelChanges();
        UserControlSettingsCoverArt.CancelChanges();
        UserControlSettingsDownloads.CancelChanges();
        UserControlSettingsGeneral.CancelChanges();
        UserControlSettingsNetwork.CancelChanges();
        UserControlSettingsPlaylist.CancelChanges();
        UserControlSettingsTags.CancelChanges();
    }

    /// <summary>
    /// Resets settings to their default values.
    /// </summary>
    private void ResetSettings()
    {
        var userSettings = _settingsService.GetUserSettings();

        // Save settings we shouldn't reset (as they're not on the Settings window)
        var downloadsPath = userSettings.DownloadsPath;
        var downloadArtistDiscography = userSettings.DownloadArtistDiscography;

        // Reset settings
        userSettings = _settingsService.ResetSettings();

        // Load back settings we shouldn't reset
        userSettings.DownloadsPath = downloadsPath;
        userSettings.DownloadArtistDiscography = downloadArtistDiscography;

        // Re-load settings on UI
        UserControlSettingsAdvanced.LoadSettings(userSettings);
        UserControlSettingsCoverArt.LoadSettings(userSettings);
        UserControlSettingsDownloads.LoadSettings(userSettings);
        UserControlSettingsGeneral.LoadSettings(userSettings);
        UserControlSettingsNetwork.LoadSettings(userSettings);
        UserControlSettingsPlaylist.LoadSettings(userSettings);
        UserControlSettingsTags.LoadSettings(userSettings);
    }

    /// <summary>
    /// Saves all settings. Returns false if any of the settings is invalid.
    /// </summary>
    private bool SaveSettings()
    {
        // Evaluate every control (no short-circuit) so that every valid value is saved.
        var isValid = UserControlSettingsAdvanced.SaveSettings();
        isValid &= UserControlSettingsCoverArt.SaveSettings();
        isValid &= UserControlSettingsDownloads.SaveSettings();
        isValid &= UserControlSettingsGeneral.SaveSettings();
        isValid &= UserControlSettingsNetwork.SaveSettings();
        isValid &= UserControlSettingsPlaylist.SaveSettings();
        isValid &= UserControlSettingsTags.SaveSettings();
        return isValid;
    }

    private void LogSettings()
    {
        var userSettingsJson = _settingsService.GetUserSettingsInJson();
        _logger.Info($"Settings saved: {userSettingsJson}");
    }
}
