using System;
using BandcampDownloader.Audio;
using BandcampDownloader.Bandcamp.Download;
using BandcampDownloader.Bandcamp.Extraction;
using BandcampDownloader.Core.Localization;
using BandcampDownloader.Core.Logging;
using BandcampDownloader.Core.Updates;
using BandcampDownloader.Helpers;
using BandcampDownloader.IO;
using BandcampDownloader.Net;
using BandcampDownloader.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace BandcampDownloader.Core.DependencyInjection;

/// <summary>
/// Wrapper around the underlying dependency injection container.
/// </summary>
internal interface IContainer
{
    T GetService<T>();
}

internal sealed class Container : IContainer
{
    private readonly ServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new container with the core services, then lets the platform-specific UI head
    /// register its own services (UI windows, platform notifications, ...).
    /// </summary>
    /// <param name="configurePlatformServices">Callback used to register platform-specific services.</param>
    public Container(Action<IServiceCollection> configurePlatformServices = null)
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddBandcampDownloaderCore();
        configurePlatformServices?.Invoke(serviceCollection);

        _serviceProvider = serviceCollection.BuildServiceProvider();
    }

    public T GetService<T>()
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}

internal static class CoreServices
{
    /// <summary>
    /// Registers every platform-independent service of BandcampDownloader.
    /// </summary>
    public static IServiceCollection AddBandcampDownloaderCore(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpClient();
        serviceCollection.AddSingleton<IAlbumInfoRetriever, AlbumInfoRetriever>();
        serviceCollection.AddSingleton<IAlbumInfoParser, AlbumInfoParser>();
        serviceCollection.AddSingleton<IAlbumUrlRetriever, AlbumUrlRetriever>();
        serviceCollection.AddSingleton<IDiscographyService, DiscographyService>();
        serviceCollection.AddSingleton<IDownloadManager, DownloadManager>();
        serviceCollection.AddSingleton<IExceptionHandler, ExceptionHandler>();
        serviceCollection.AddSingleton<IFileService, FileService>();
        serviceCollection.AddSingleton<IHttpService, HttpService>();
        serviceCollection.AddSingleton<IImageService, ImageService>();
        serviceCollection.AddSingleton<ILanguageService, LanguageService>();
        serviceCollection.AddSingleton<ILoggingService, LoggingService>();
        serviceCollection.AddSingleton<IPlaylistCreator, PlaylistCreator>();
        serviceCollection.AddSingleton<IResilienceService, ResilienceService>();
        serviceCollection.AddSingleton<ISettingsService, SettingsService>();
        serviceCollection.AddSingleton<ITagService, TagService>();
        serviceCollection.AddSingleton<ITrackFileService, TrackFileService>();
        serviceCollection.AddSingleton<IUpdatesService, UpdatesService>();

        return serviceCollection;
    }
}
