using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BandcampDownloader.Avalonia.Core.Localization;
using BandcampDownloader.Avalonia.Core.Theme;
using BandcampDownloader.Avalonia.Platform;
using BandcampDownloader.Core;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Core.Localization;
using BandcampDownloader.Core.Logging;
using BandcampDownloader.Core.Notifications;
using BandcampDownloader.Settings;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace BandcampDownloader.Avalonia;

internal sealed class App : Application
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public override void Initialize()
    {
        // Shown in the macOS menu bar and in the "About" panel: CFBundleName alone is not enough
        // (verified: the menu bar shows "Avalonia Application" otherwise).
        Name = "Bandcamp Downloader";

        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var container = DependencyInjectionHelper.InitializeContainer(ConfigureAvaloniaServices);

            // 1. Initialize the logger first in order to be able to log any early errors
            var loggingService = container.GetService<ILoggingService>();
            loggingService.InitializeLogger();

            // 2. Register the handler for unhandled exception in order to catch and log early unhandled exceptions
            var exceptionHandler = container.GetService<IExceptionHandler>();
            exceptionHandler.RegisterUnhandledExceptionHandler();

            LogAppProperties();

            // 3. Initialize settings, language and theme before creating any window: some services read
            // the user settings when they are constructed.
            var settingsService = container.GetService<ISettingsService>();
            var userSettings = settingsService.InitializeSettings();

            var languageService = container.GetService<ILanguageService>();
            Localizer.Instance.Initialize(languageService);
            languageService.ApplyLanguage(userSettings.Language);

            var themeService = container.GetService<IThemeService>();
            themeService.ApplySkin(userSettings.Theme);

            LogUserSettings(settingsService);

            // 4. Open the main window
            var mainWindow = container.GetService<MainWindow>();
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Registers the services specific to the Avalonia head of the application.
    /// </summary>
    private static void ConfigureAvaloniaServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IUserNotifier, AvaloniaUserNotifier>();
        serviceCollection.AddSingleton<IThemeService, AvaloniaThemeService>();
        serviceCollection.AddSingleton<MainWindow>();
    }

    private static void LogAppProperties()
    {
        _logger.Info("┳┓     ┓          ┳┓       ┓     ┓");
        _logger.Info("┣┫┏┓┏┓┏┫┏┏┓┏┳┓┏┓  ┃┃┏┓┓┏┏┏┓┃┏┓┏┓┏┫┏┓┏┓");
        _logger.Info("┻┛┗┻┛┗┗┻┗┗┻┛┗┗┣┛  ┻┛┗┛┗┻┛┛┗┗┗┛┗┻┗┻┗ ┛");
        _logger.Info("              ┛");
        _logger.Info($"BandcampDownloader version: {Constants.APP_VERSION_FORMATTED}");
        _logger.Info($"Framework description: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        _logger.Info($"OS architecture: {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}");
        _logger.Info($"OS description: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        _logger.Info($"Process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        _logger.Info($"Runtime identifier: {System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier}");
    }

    private static void LogUserSettings(ISettingsService settingsService)
    {
        try
        {
            _logger.Info($"Settings: {settingsService.GetUserSettingsInJson()}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Could not log user settings");
        }
    }
}
