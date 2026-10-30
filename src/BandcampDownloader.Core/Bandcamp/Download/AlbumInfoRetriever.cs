using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BandcampDownloader.Bandcamp.Extraction;
using BandcampDownloader.Model;
using BandcampDownloader.Net;
using NLog;

namespace BandcampDownloader.Bandcamp.Download;

internal interface IAlbumInfoRetriever
{
    Task<IReadOnlyCollection<Album>> GetAlbumsAsync(IReadOnlyCollection<string> albumsUrls, CancellationToken cancellationToken);
    event DownloadProgressChangedEventHandler DownloadProgressChanged;
}

internal sealed class AlbumInfoRetriever : IAlbumInfoRetriever
{
    private readonly IAlbumInfoParser _albumInfoParser;
    private readonly IBandcampApiService _bandcampApiService;
    private readonly IHttpService _httpService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    public event DownloadProgressChangedEventHandler DownloadProgressChanged;

    public AlbumInfoRetriever(IAlbumInfoParser albumInfoParser, IBandcampApiService bandcampApiService, IHttpService httpService)
    {
        _albumInfoParser = albumInfoParser;
        _bandcampApiService = bandcampApiService;
        _httpService = httpService;
    }

    public async Task<IReadOnlyCollection<Album>> GetAlbumsAsync(IReadOnlyCollection<string> albumsUrls, CancellationToken cancellationToken)
    {
        var albums = new List<Album>();

        foreach (var url in albumsUrls)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedArgs($"Retrieving album data for {url}", DownloadProgressChangedLevel.Info));

            // Retrieve URL HTML source code
            string htmlContent;
            try
            {
                DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedArgs($"Downloading album info from url: {url}", DownloadProgressChangedLevel.VerboseInfo));
                var httpClient = _httpService.CreateHttpClient();
                htmlContent = await httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Error(ex, $"Error downloading album info from url: {url}");
                DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedArgs($"Could not retrieve data for {url}", DownloadProgressChangedLevel.Error));
                continue;
            }

            // Get info on album
            try
            {
                var album = _albumInfoParser.GetAlbumInfoFromAlbumPage(htmlContent, cancellationToken);

                // The page data does not always contain every track / MP3 URL (purchase-only tracks,
                // public excerpts, ...): complete it from the Bandcamp API when it is incomplete.
                await EnrichAlbumAsync(album, cancellationToken).ConfigureAwait(false);
                RemoveUnavailableTracks(album);

                if (album.Tracks.Count > 0)
                {
                    albums.Add(album);
                }
                else
                {
                    DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedArgs(
                        $"No downloadable track could be found for {url}. Bandcamp does not provide a stream for this page " +
                        "(the release may be purchase-only, streaming-disabled, or the tracks may only be available as excerpts)",
                        DownloadProgressChangedLevel.Warning));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Error(ex, $"Could not retrieve album info for {url}");
                DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedArgs($"Could not retrieve album info for {url}", DownloadProgressChangedLevel.Error));
            }
        }

        return albums;
    }

    /// <summary>
    /// Completes the specified album with the tracks returned by the Bandcamp API when the page data is
    /// incomplete (missing MP3 URLs or missing tracks).
    /// </summary>
    private async Task EnrichAlbumAsync(Album album, CancellationToken cancellationToken)
    {
        var isPageDataComplete = album.Tracks.Count > 0 && album.Tracks.All(track => !string.IsNullOrWhiteSpace(track.Mp3Url));
        if (isPageDataComplete ||
            album.BandId is not > 0 ||
            album.TralbumId is not > 0 ||
            string.IsNullOrWhiteSpace(album.TralbumType))
        {
            return;
        }

        try
        {
            DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedArgs(
                $"Retrieving the missing track information from the Bandcamp API for \"{album.Title}\"",
                DownloadProgressChangedLevel.VerboseInfo));

            var apiTracks = await _bandcampApiService
                .GetTracksAsync(album.BandId.Value, album.TralbumId.Value, album.TralbumType, cancellationToken)
                .ConfigureAwait(false);
            var enrichedCount = AlbumEnricher.Enrich(album, apiTracks);

            if (enrichedCount > 0)
            {
                DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedArgs(
                    $"Retrieved {enrichedCount} additional track(s) or download URL(s) from the Bandcamp API for \"{album.Title}\"",
                    DownloadProgressChangedLevel.Info));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warn(ex, $"Could not complete the track information from the Bandcamp API for \"{album.Title}\"");
        }
    }

    /// <summary>
    /// Removes the tracks that Bandcamp does not expose for streaming/downloading, logging a warning for
    /// each of them instead of failing the whole download.
    /// </summary>
    private void RemoveUnavailableTracks(Album album)
    {
        foreach (var track in album.Tracks.Where(track => string.IsNullOrWhiteSpace(track.Mp3Url)).ToList())
        {
            album.Tracks.Remove(track);
            _logger.Warn($"Track \"{track.Title}\" is not available for streaming or download on Bandcamp - skipping it");
            DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedArgs(
                $"Track \"{track.Title}\" is not available for streaming or download on Bandcamp - skipping it",
                DownloadProgressChangedLevel.Warning));
        }
    }
}
