using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Avalonia.Core.Theme;
using BandcampDownloader.Avalonia.UI.Shared;
using BandcampDownloader.Avalonia.UI.Update;
using BandcampDownloader.Core;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Core.Localization;
using BandcampDownloader.Core.Updates;
using BandcampDownloader.Settings;
using NLog;

namespace BandcampDownloader.Avalonia.UI.Settings;

internal sealed partial class UserControlSettingsGeneral : UserControl, IUserControlSettings
{
    private readonly ILanguageService _languageService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IUpdatesService _updatesService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private bool _isLoading;

    public UserControlSettingsGeneral()
    {
        _languageService = DependencyInjectionHelper.GetService<ILanguageService>();
        _settingsService = DependencyInjectionHelper.GetService<ISettingsService>();
        _themeService = DependencyInjectionHelper.GetService<IThemeService>();
        _updatesService = DependencyInjectionHelper.GetService<IUpdatesService>();

        InitializeComponent();

        // Composed tooltip: localized explanation followed by the address queried on startup.
        TextBlockCheckForUpdatesToolTipUrl.Text = Constants.URL_LATEST_RELEASE;
        Hyperlink.MakeLookLikeLink(TextBlockTranslationHelp);
        ToolTip.SetTip(TextBlockTranslationHelp, Constants.URL_HELP_TRANSLATE);
        TextBlockTranslationHelp.PointerPressed += (_, _) => Hyperlink.OpenUrl(Constants.URL_HELP_TRANSLATE);

        // The theme descriptions are localized: rebuild them when the language changes.
        _languageService.CultureChanged += LanguageService_CultureChanged;
        DetachedFromVisualTree += (_, _) => _languageService.CultureChanged -= LanguageService_CultureChanged;

        LoadSettings(_settingsService.GetUserSettings());
    }

    /// <summary>
    /// Cancels the changes already applied.
    /// </summary>
    public void CancelChanges()
    {
        var userSettings = _settingsService.GetUserSettings();

        // Revert the language only if it has been changed
        if (ComboBoxLanguage.SelectedItem is EnumOption<Language> language && language.Value != userSettings.Language)
        {
            _languageService.ApplyLanguage(userSettings.Language);
        }

        // Revert the theme only if it has been changed
        if (ComboBoxTheme.SelectedItem is EnumOption<Skin> theme && theme.Value != userSettings.Theme)
        {
            _themeService.ApplySkin(userSettings.Theme);
        }
    }

    /// <summary>
    /// Loads settings from the specified user settings.
    /// </summary>
    public void LoadSettings(IUserSettings userSettings)
    {
        _isLoading = true;

        // Re-load the data context in case the settings have changed (Config.Net settings do not
        // raise change notifications, so every control is also populated explicitly below).
        DataContext = userSettings;

        ComboBoxLanguage.ItemsSource = EnumOption<Language>.All();
        ComboBoxLanguage.SelectedItem = EnumOption<Language>.FromValue(userSettings.Language);

        var themeOptions = CreateThemeOptions();
        ComboBoxTheme.ItemsSource = themeOptions;
        ComboBoxTheme.SelectedItem = themeOptions.First(option => option.Value == userSettings.Theme);

        CheckBoxCheckForUpdates.IsChecked = userSettings.CheckForUpdates;
        CheckBoxEnableApplicationSounds.IsChecked = userSettings.EnableApplicationSounds;
        CheckBoxVerboseLog.IsChecked = userSettings.ShowVerboseLog;

        _isLoading = false;

        // Keep the applied language and theme in sync with the loaded settings (e.g. after a reset).
        _languageService.ApplyLanguage(userSettings.Language);
        _themeService.ApplySkin(userSettings.Theme);
    }

    /// <summary>
    /// Saves settings to the user settings.
    /// </summary>
    public bool SaveSettings()
    {
        var userSettings = _settingsService.GetUserSettings();

        userSettings.CheckForUpdates = CheckBoxCheckForUpdates.IsChecked == true;
        userSettings.EnableApplicationSounds = CheckBoxEnableApplicationSounds.IsChecked == true;
        userSettings.ShowVerboseLog = CheckBoxVerboseLog.IsChecked == true;

        if (ComboBoxLanguage.SelectedItem is EnumOption<Language> language)
        {
            userSettings.Language = language.Value;
        }

        if (ComboBoxTheme.SelectedItem is EnumOption<Skin> theme)
        {
            userSettings.Theme = theme.Value;
        }

        return true;
    }

    private async void ButtonCheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        Version latestVersion;
        try
        {
            latestVersion = await _updatesService.GetLatestVersionAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to get latest version");
            await ShowMessageAsync(Localizer.Instance["messageBoxCheckForUpdatesError"], MessageBoxIcon.Error);
            return;
        }

        if (latestVersion.IsNewerVersion())
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
            {
                var windowUpdate = new WindowUpdate();
                windowUpdate.Show(owner);
            }
        }
        else
        {
            var message = string.Format(Localizer.Instance["messageBoxNoUpdateAvailable"], Constants.APP_VERSION_FORMATTED);
            await ShowMessageAsync(message, MessageBoxIcon.Info);
        }
    }

    private void ComboBoxLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (ComboBoxLanguage.SelectedItem is EnumOption<Language> language)
        {
            _languageService.ApplyLanguage(language.Value);
        }
    }

    private void ComboBoxTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (ComboBoxTheme.SelectedItem is EnumOption<Skin> theme)
        {
            _themeService.ApplySkin(theme.Value);
        }
    }

    private void LanguageService_CultureChanged(object sender, EventArgs e)
    {
        var selectedTheme = (ComboBoxTheme.SelectedItem as EnumOption<Skin>)?.Value;
        var themeOptions = CreateThemeOptions();

        _isLoading = true;
        ComboBoxTheme.ItemsSource = themeOptions;
        if (selectedTheme.HasValue)
        {
            ComboBoxTheme.SelectedItem = themeOptions.First(option => option.Value == selectedTheme.Value);
        }

        _isLoading = false;
    }

    private async System.Threading.Tasks.Task ShowMessageAsync(string message, MessageBoxIcon icon)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await MessageBox.ShowAsync(owner, message, MessageBoxButtons.Ok, icon, "Bandcamp Downloader");
        }
    }

    /// <summary>
    /// Builds the theme options with a description localized in the current language.
    /// </summary>
    private static IReadOnlyList<EnumOption<Skin>> CreateThemeOptions()
    {
        return EnumOption<Skin>.All().Select(option => new EnumOption<Skin>(option.Value)).ToList();
    }
}
