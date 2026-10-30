using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Core.Localization;
using BandcampDownloader.Settings;
using NLog;

namespace BandcampDownloader.Avalonia.UI.Settings;

internal sealed partial class UserControlSettingsTags : UserControl, IUserControlSettings
{
    private readonly ILanguageService _languageService;
    private readonly ISettingsService _settingsService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public UserControlSettingsTags()
    {
        _languageService = DependencyInjectionHelper.GetService<ILanguageService>();
        _settingsService = DependencyInjectionHelper.GetService<ISettingsService>();

        InitializeComponent();

        // The tag action descriptions are localized: rebuild them when the language changes.
        _languageService.CultureChanged += LanguageService_CultureChanged;
        DetachedFromVisualTree += (_, _) => _languageService.CultureChanged -= LanguageService_CultureChanged;

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

        var editOptions = CreateOptions<TagEditAction>();
        var removeOptions = CreateOptions<TagRemoveAction>();

        ComboBoxArtist.ItemsSource = editOptions;
        ComboBoxArtist.SelectedItem = editOptions.First(option => option.Value == userSettings.TagArtist);
        ComboBoxTrackNumber.ItemsSource = editOptions;
        ComboBoxTrackNumber.SelectedItem = editOptions.First(option => option.Value == userSettings.TagTrackNumber);
        ComboBoxTrackTitle.ItemsSource = editOptions;
        ComboBoxTrackTitle.SelectedItem = editOptions.First(option => option.Value == userSettings.TagTrackTitle);
        ComboBoxAlbumTitle.ItemsSource = editOptions;
        ComboBoxAlbumTitle.SelectedItem = editOptions.First(option => option.Value == userSettings.TagAlbumTitle);
        ComboBoxAlbumArtist.ItemsSource = editOptions;
        ComboBoxAlbumArtist.SelectedItem = editOptions.First(option => option.Value == userSettings.TagAlbumArtist);
        ComboBoxYear.ItemsSource = editOptions;
        ComboBoxYear.SelectedItem = editOptions.First(option => option.Value == userSettings.TagYear);
        ComboBoxLyrics.ItemsSource = editOptions;
        ComboBoxLyrics.SelectedItem = editOptions.First(option => option.Value == userSettings.TagLyrics);
        ComboBoxComments.ItemsSource = removeOptions;
        ComboBoxComments.SelectedItem = removeOptions.First(option => option.Value == userSettings.TagComments);

        CheckBoxModifyTags.IsChecked = userSettings.ModifyTags;
        TextBoxFileNameFormat.Text = userSettings.FileNameFormat;
    }

    /// <summary>
    /// Saves settings to the user settings. Returns false if the user input is invalid.
    /// </summary>
    public bool SaveSettings()
    {
        var userSettings = _settingsService.GetUserSettings();

        userSettings.ModifyTags = CheckBoxModifyTags.IsChecked == true;

        if (ComboBoxArtist.SelectedItem is EnumOption<TagEditAction> artist)
        {
            userSettings.TagArtist = artist.Value;
        }

        if (ComboBoxTrackNumber.SelectedItem is EnumOption<TagEditAction> trackNumber)
        {
            userSettings.TagTrackNumber = trackNumber.Value;
        }

        if (ComboBoxTrackTitle.SelectedItem is EnumOption<TagEditAction> trackTitle)
        {
            userSettings.TagTrackTitle = trackTitle.Value;
        }

        if (ComboBoxAlbumTitle.SelectedItem is EnumOption<TagEditAction> albumTitle)
        {
            userSettings.TagAlbumTitle = albumTitle.Value;
        }

        if (ComboBoxAlbumArtist.SelectedItem is EnumOption<TagEditAction> albumArtist)
        {
            userSettings.TagAlbumArtist = albumArtist.Value;
        }

        if (ComboBoxYear.SelectedItem is EnumOption<TagEditAction> year)
        {
            userSettings.TagYear = year.Value;
        }

        if (ComboBoxLyrics.SelectedItem is EnumOption<TagEditAction> lyrics)
        {
            userSettings.TagLyrics = lyrics.Value;
        }

        if (ComboBoxComments.SelectedItem is EnumOption<TagRemoveAction> comments)
        {
            userSettings.TagComments = comments.Value;
        }

        // The file name format must end with ".mp3" (WPF Mp3ExtensionRule).
        var fileNameFormat = TextBoxFileNameFormat.Text ?? string.Empty;
        if (fileNameFormat.Length < 4 || !fileNameFormat.EndsWith(".mp3", StringComparison.Ordinal))
        {
            ShowInvalidInputMessage($"{Localizer.Instance["labelFileNameFormat"]}: must end with \".mp3\".");
            return false;
        }

        userSettings.FileNameFormat = fileNameFormat;

        return true;
    }

    private void LanguageService_CultureChanged(object sender, EventArgs e)
    {
        var editOptions = CreateOptions<TagEditAction>();
        var removeOptions = CreateOptions<TagRemoveAction>();

        RefreshComboBoxOptions(ComboBoxArtist, editOptions);
        RefreshComboBoxOptions(ComboBoxTrackNumber, editOptions);
        RefreshComboBoxOptions(ComboBoxTrackTitle, editOptions);
        RefreshComboBoxOptions(ComboBoxAlbumTitle, editOptions);
        RefreshComboBoxOptions(ComboBoxAlbumArtist, editOptions);
        RefreshComboBoxOptions(ComboBoxYear, editOptions);
        RefreshComboBoxOptions(ComboBoxLyrics, editOptions);
        RefreshComboBoxOptions(ComboBoxComments, removeOptions);
    }

    /// <summary>
    /// Rebuilds the items of the specified ComboBox while keeping the selected value.
    /// </summary>
    private static void RefreshComboBoxOptions<T>(ComboBox comboBox, IReadOnlyList<EnumOption<T>> options) where T : struct, Enum
    {
        var selectedValue = (comboBox.SelectedItem as EnumOption<T>)?.Value;
        comboBox.ItemsSource = options;
        if (selectedValue.HasValue)
        {
            comboBox.SelectedItem = options.First(option => EqualityComparer<T>.Default.Equals(option.Value, selectedValue.Value));
        }
    }

    /// <summary>
    /// Builds the enumeration options with a description localized in the current language.
    /// </summary>
    private static IReadOnlyList<EnumOption<T>> CreateOptions<T>() where T : struct, Enum
    {
        return EnumOption<T>.All().Select(option => new EnumOption<T>(option.Value)).ToList();
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
