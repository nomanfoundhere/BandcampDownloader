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

internal sealed partial class UserControlSettingsCoverArt : UserControl, IUserControlSettings
{
    private readonly ISettingsService _settingsService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public UserControlSettingsCoverArt()
    {
        _settingsService = DependencyInjectionHelper.GetService<ISettingsService>();

        InitializeComponent();

        // The maximum sizes are only editable when the cover art is saved (and resized).
        CheckBoxSaveCoverArtInFolder.IsCheckedChanged += (_, _) => UpdateMaxSizesEnabledStates();
        CheckBoxCoverArtInFolderResize.IsCheckedChanged += (_, _) => UpdateMaxSizesEnabledStates();
        CheckBoxSaveCoverArtInTags.IsCheckedChanged += (_, _) => UpdateMaxSizesEnabledStates();
        CheckBoxCoverArtInTagsResize.IsCheckedChanged += (_, _) => UpdateMaxSizesEnabledStates();

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

        CheckBoxSaveCoverArtInFolder.IsChecked = userSettings.SaveCoverArtInFolder;
        TextBoxCoverArtFileNameFormat.Text = userSettings.CoverArtFileNameFormat;
        CheckBoxCoverArtInFolderConvertToJpg.IsChecked = userSettings.CoverArtInFolderConvertToJpg;
        CheckBoxCoverArtInFolderResize.IsChecked = userSettings.CoverArtInFolderResize;
        TextBoxCoverArtInFolderMaxSize.Text = userSettings.CoverArtInFolderMaxSize.ToString(CultureInfo.InvariantCulture);

        CheckBoxSaveCoverArtInTags.IsChecked = userSettings.SaveCoverArtInTags;
        CheckBoxCoverArtInTagsConvertToJpg.IsChecked = userSettings.CoverArtInTagsConvertToJpg;
        CheckBoxCoverArtInTagsResize.IsChecked = userSettings.CoverArtInTagsResize;
        TextBoxCoverArtInTagsMaxSize.Text = userSettings.CoverArtInTagsMaxSize.ToString(CultureInfo.InvariantCulture);

        UpdateMaxSizesEnabledStates();
    }

    /// <summary>
    /// Saves settings to the user settings. Returns false if the user input is invalid.
    /// </summary>
    public bool SaveSettings()
    {
        var userSettings = _settingsService.GetUserSettings();
        var errors = new List<string>();

        userSettings.SaveCoverArtInFolder = CheckBoxSaveCoverArtInFolder.IsChecked == true;
        userSettings.CoverArtFileNameFormat = TextBoxCoverArtFileNameFormat.Text ?? string.Empty;
        userSettings.CoverArtInFolderConvertToJpg = CheckBoxCoverArtInFolderConvertToJpg.IsChecked == true;
        userSettings.CoverArtInFolderResize = CheckBoxCoverArtInFolderResize.IsChecked == true;

        if (int.TryParse(TextBoxCoverArtInFolderMaxSize.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var coverArtInFolderMaxSize))
        {
            userSettings.CoverArtInFolderMaxSize = coverArtInFolderMaxSize;
        }
        else
        {
            errors.Add($"{Localizer.Instance["labelCoverArtInFolderMaxSize"]}: not a numeric value");
        }

        userSettings.SaveCoverArtInTags = CheckBoxSaveCoverArtInTags.IsChecked == true;
        userSettings.CoverArtInTagsConvertToJpg = CheckBoxCoverArtInTagsConvertToJpg.IsChecked == true;
        userSettings.CoverArtInTagsResize = CheckBoxCoverArtInTagsResize.IsChecked == true;

        if (int.TryParse(TextBoxCoverArtInTagsMaxSize.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var coverArtInTagsMaxSize))
        {
            userSettings.CoverArtInTagsMaxSize = coverArtInTagsMaxSize;
        }
        else
        {
            errors.Add($"{Localizer.Instance["labelCoverArtInTagsMaxSize"]}: not a numeric value");
        }

        if (errors.Count > 0)
        {
            ShowInvalidInputMessage(string.Join(Environment.NewLine, errors));
            return false;
        }

        return true;
    }

    /// <summary>
    /// The cover art maximum sizes are only enabled when the cover art is saved and resized.
    /// </summary>
    private void UpdateMaxSizesEnabledStates()
    {
        TextBoxCoverArtInFolderMaxSize.IsEnabled = CheckBoxSaveCoverArtInFolder.IsChecked == true && CheckBoxCoverArtInFolderResize.IsChecked == true;
        TextBoxCoverArtInTagsMaxSize.IsEnabled = CheckBoxSaveCoverArtInTags.IsChecked == true && CheckBoxCoverArtInTagsResize.IsChecked == true;
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
