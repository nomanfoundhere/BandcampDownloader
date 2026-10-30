using Avalonia.Controls;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Settings;

namespace BandcampDownloader.Avalonia.UI.Settings;

internal sealed partial class UserControlSettingsPlaylist : UserControl, IUserControlSettings
{
    private readonly ISettingsService _settingsService;

    public UserControlSettingsPlaylist()
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

        CheckBoxCreatePlaylist.IsChecked = userSettings.CreatePlaylist;

        ComboBoxPlaylistFormat.ItemsSource = EnumOption<PlaylistFormat>.All();
        ComboBoxPlaylistFormat.SelectedItem = EnumOption<PlaylistFormat>.FromValue(userSettings.PlaylistFormat);

        TextBoxPlaylistFileNameFormat.Text = userSettings.PlaylistFileNameFormat;
        CheckBoxM3UExtended.IsChecked = userSettings.M3uExtended;

        UpdatePlaylistFileNameExtension();
    }

    /// <summary>
    /// Saves settings to the user settings.
    /// </summary>
    public bool SaveSettings()
    {
        var userSettings = _settingsService.GetUserSettings();

        userSettings.CreatePlaylist = CheckBoxCreatePlaylist.IsChecked == true;
        userSettings.M3uExtended = CheckBoxM3UExtended.IsChecked == true;
        userSettings.PlaylistFileNameFormat = TextBoxPlaylistFileNameFormat.Text ?? string.Empty;

        if (ComboBoxPlaylistFormat.SelectedItem is EnumOption<PlaylistFormat> playlistFormat)
        {
            userSettings.PlaylistFormat = playlistFormat.Value;
        }

        return true;
    }

    private void ComboBoxPlaylistFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePlaylistFileNameExtension();
    }

    /// <summary>
    /// Displays the extension matching the selected playlist format (".m3u", ".pls", ...).
    /// </summary>
    private void UpdatePlaylistFileNameExtension()
    {
        if (ComboBoxPlaylistFormat.SelectedItem is EnumOption<PlaylistFormat> playlistFormat)
        {
            LabelPlaylistFileNameExtension.Content = $".{playlistFormat.Value}";
        }
    }
}
