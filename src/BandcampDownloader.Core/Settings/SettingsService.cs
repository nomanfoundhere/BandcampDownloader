using System;
using System.IO;
using System.Text.Json;
using BandcampDownloader.Platform;
using Config.Net;
using NLog;

namespace BandcampDownloader.Settings;

internal interface ISettingsService
{
    IUserSettings InitializeSettings();
    IUserSettings ResetSettings();
    IUserSettings GetUserSettings();
    string GetUserSettingsInJson();
}

public sealed class SettingsService : ISettingsService
{
    /// <summary>
    /// The absolute path to the settings file.
    /// </summary>
    private readonly string _userSettingsFilePath = AppPaths.SettingsFilePath;

    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// True once the legacy settings migration has been attempted in this process. It must only be
    /// attempted once: ResetSettings() deletes the settings file then re-initializes the settings, and
    /// migrating again would silently restore the old (migrated) settings instead of the defaults.
    /// </summary>
    private bool _legacySettingsMigrationAttempted;

    private IUserSettings _userSettings;

    public IUserSettings InitializeSettings()
    {
        MigrateLegacySettingsIfNeeded();

        _userSettings = new ConfigurationBuilder<IUserSettings>().UseIniFile(_userSettingsFilePath).Build();

        if (string.IsNullOrEmpty(_userSettings.DownloadsPath))
        {
            // Its default value cannot be set in settings as it isn't determined by a constant function
            _userSettings.DownloadsPath = AppPaths.DefaultDownloadsPathFormat;
        }

        return _userSettings;
    }

    public IUserSettings ResetSettings()
    {
        File.Delete(_userSettingsFilePath);
        _userSettings = InitializeSettings();

        return _userSettings;
    }

    /// <summary>
    /// Copies the settings file of versions up to 1.5.2 (stored next to the application) to the new
    /// per-user application data directory, so that upgrading does not lose the user's settings.
    /// Best effort: a failure to migrate must never prevent the application from starting.
    /// </summary>
    private void MigrateLegacySettingsIfNeeded()
    {
        if (_legacySettingsMigrationAttempted)
        {
            return;
        }

        _legacySettingsMigrationAttempted = true;

        try
        {
            var legacySettingsFilePath = AppPaths.LegacySettingsFilePath;
            if (legacySettingsFilePath == null ||
                AppPaths.IsDataDirectoryOverridden ||
                !File.Exists(legacySettingsFilePath) ||
                File.Exists(_userSettingsFilePath))
            {
                return;
            }

            Directory.CreateDirectory(AppPaths.AppDataDirectory);

            // Copy through a temporary file so that an interrupted migration never leaves a partial
            // settings file behind (which would then block any future migration).
            var temporaryFilePath = _userSettingsFilePath + ".migration.tmp";
            File.Copy(legacySettingsFilePath, temporaryFilePath, overwrite: true);
            File.Move(temporaryFilePath, _userSettingsFilePath, overwrite: true);

            // Rename the legacy file so that it can never be imported again (settings reset, portable
            // run, ...). If the rename fails, the existing settings file still prevents re-import.
            try
            {
                File.Move(legacySettingsFilePath, legacySettingsFilePath + ".migrated", overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, $"Could not rename the legacy settings file {legacySettingsFilePath}");
            }

            _logger.Info($"Migrated the settings from {legacySettingsFilePath} to {_userSettingsFilePath}");
        }
        catch (Exception ex)
        {
            // The application will simply start with default settings.
            _logger.Warn(ex, "Could not migrate the legacy settings file");
        }
    }

    public IUserSettings GetUserSettings()
    {
        return _userSettings;
    }

    public string GetUserSettingsInJson()
    {
        var serializedSettings = JsonSerializer.Serialize(_userSettings);
        return serializedSettings;
    }
}
