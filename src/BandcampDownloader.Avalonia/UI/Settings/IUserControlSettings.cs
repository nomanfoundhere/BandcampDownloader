using BandcampDownloader.Settings;

namespace BandcampDownloader.Avalonia.UI.Settings;

internal interface IUserControlSettings
{
    void CancelChanges();

    void LoadSettings(IUserSettings userSettings);

    /// <summary>
    /// Saves the settings. Returns false if the user input is invalid (in which case the settings
    /// window stays open so the user can fix the input).
    /// </summary>
    bool SaveSettings();
}
