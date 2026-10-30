using System;
using System.Collections.Generic;
using System.Linq;
using BandcampDownloader.Model;

namespace BandcampDownloader.Bandcamp.Extraction;

/// <summary>
/// Completes the album parsed from a Bandcamp HTML page with the tracks returned by the Bandcamp API.
/// </summary>
internal static class AlbumEnricher
{
    /// <summary>
    /// Merges the API tracks into the specified album: fills the missing MP3 URLs and adds the
    /// streamable tracks that the page data omitted (for instance a public excerpt next to
    /// purchase-only full tracks). Tracks that Bandcamp does not expose for streaming are ignored:
    /// they will be skipped with a warning by the caller.
    /// </summary>
    /// <returns>The number of tracks added or completed.</returns>
    public static int Enrich(Album album, IReadOnlyCollection<BandcampApiTrack> apiTracks)
    {
        ArgumentNullException.ThrowIfNull(album);

        if (apiTracks == null || apiTracks.Count == 0)
        {
            return 0;
        }

        var enrichedCount = 0;

        foreach (var apiTrack in apiTracks)
        {
            if (string.IsNullOrWhiteSpace(apiTrack.Mp3Url))
            {
                // Bandcamp does not stream this track (purchase-only, unreleased, ...).
                continue;
            }

            var existingTrack = album.Tracks.FirstOrDefault(track => track.TrackId != 0 && track.TrackId == apiTrack.TrackId);
            if (existingTrack != null)
            {
                if (string.IsNullOrWhiteSpace(existingTrack.Mp3Url))
                {
                    existingTrack.Mp3Url = apiTrack.Mp3Url;
                    enrichedCount++;
                }

                continue;
            }

            // The track is streamable but was missing from the page data (or had no MP3 URL at all).
            var trackNumber = apiTrack.Number ?? album.Tracks.Count + 1;
            var track = new Track(album, apiTrack.Duration, null, apiTrack.Mp3Url, trackNumber, apiTrack.Title)
            {
                TrackId = apiTrack.TrackId,
            };
            album.Tracks.Add(track);
            enrichedCount++;
        }

        if (enrichedCount > 0)
        {
            // The added tracks may not be at the end of the album (e.g. a bonus track in the middle).
            album.Tracks.Sort((first, second) => first.Number.CompareTo(second.Number));
        }

        return enrichedCount;
    }
}
