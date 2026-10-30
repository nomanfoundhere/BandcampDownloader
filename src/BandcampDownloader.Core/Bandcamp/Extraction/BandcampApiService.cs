using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BandcampDownloader.Bandcamp.Extraction.Dto;
using BandcampDownloader.Net;
using NLog;

namespace BandcampDownloader.Bandcamp.Extraction;

/// <summary>
/// A track of an album/track page, as returned by the Bandcamp API, with its streaming URL when
/// Bandcamp exposes one.
/// </summary>
internal sealed class BandcampApiTrack
{
    public long TrackId { get; init; }
    public string Title { get; init; }
    public int? Number { get; init; }
    public double Duration { get; init; }
    public string Mp3Url { get; init; }
}

internal interface IBandcampApiService
{
    /// <summary>
    /// Returns the tracks of the specified album/track, including their streaming URLs (which may be
    /// null when Bandcamp does not allow streaming/downloading that track).
    /// </summary>
    /// <param name="bandId">The Bandcamp id of the band owning the tralbum.</param>
    /// <param name="tralbumId">The tralbum id (album id or track id, depending on tralbumType).</param>
    /// <param name="tralbumType">The tralbum type: "album" or "track" (the short "a"/"t" forms are also accepted).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<BandcampApiTrack>> GetTracksAsync(long bandId, long tralbumId, string tralbumType, CancellationToken cancellationToken);
}

/// <summary>
/// Queries the public JSON API used by the Bandcamp mobile applications.
/// </summary>
/// <remarks>
/// The data embedded in the HTML page (<c>data-tralbum</c>) is not always complete: for some releases
/// the full track is purchase-only (so the page data has no MP3 URL), while a public excerpt or other
/// tracks are streamable. The API gives the authoritative list of tracks with their stream URLs, which
/// lets us download what Bandcamp actually exposes and clearly skip what it does not.
/// </remarks>
internal sealed class BandcampApiService : IBandcampApiService
{
    private const string API_URL = "https://bandcamp.com/api/mobile/24/tralbum_details";

    private readonly IHttpService _httpService;
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public BandcampApiService(IHttpService httpService)
    {
        _httpService = httpService;
    }

    public async Task<IReadOnlyList<BandcampApiTrack>> GetTracksAsync(long bandId, long tralbumId, string tralbumType, CancellationToken cancellationToken)
    {
        var apiTralbumType = tralbumType switch
        {
            "album" or "a" => "a",
            "track" or "t" => "t",
            _ => null,
        };

        if (apiTralbumType == null)
        {
            _logger.Warn($"Unknown tralbum type \"{tralbumType}\": cannot query the Bandcamp API");
            return [];
        }

        var url = $"{API_URL}?band_id={bandId}&tralbum_type={apiTralbumType}&tralbum_id={tralbumId}";

        try
        {
            using var httpClient = _httpService.CreateHttpClient();
            var json = await httpClient.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            var details = JsonSerializer.Deserialize<JsonTralbumDetails>(json);

            if (details == null || details.IsError)
            {
                _logger.Warn($"The Bandcamp API returned an error for {url}");
                return [];
            }

            var apiTracks = (details.Tracks ?? []).Where(t => t.TrackId != null).Select(t => new BandcampApiTrack
            {
                TrackId = t.TrackId!.Value,
                Title = t.Title,
                Number = t.TrackNumber,
                Duration = t.Duration,
                Mp3Url = NormalizeUrl(t.StreamingUrl?.Url),
            }).ToList();

            _logger.Debug($"Retrieved {apiTracks.Count} track(s) from the Bandcamp API for {url}");

            return apiTracks;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Warn(ex, $"Could not retrieve track information from the Bandcamp API for {url}");
            return [];
        }
    }

    /// <summary>
    /// Returns the specified URL with a scheme ("//example.com" URLs lack a protocol).
    /// </summary>
    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        return url.StartsWith("//", StringComparison.Ordinal) ? "https:" + url : url;
    }
}
