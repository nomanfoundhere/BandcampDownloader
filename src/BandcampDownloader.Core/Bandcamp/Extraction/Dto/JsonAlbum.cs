using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using BandcampDownloader.Model;

namespace BandcampDownloader.Bandcamp.Extraction.Dto;

internal sealed class JsonAlbum
{
    private const string URL_END = "_0.jpg";
    private const string URL_START = "https://f4.bcbits.com/img/a"; // Uses the art_id variable to retrieve the image from Bandcamp hosting site

    [JsonPropertyName("current")]
    public JsonAlbumData AlbumData { get; init; }

    /// <summary>
    /// The id of the album (when this page is an album) or of the track (when this page is a track).
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    [JsonPropertyName("art_id")]
    public long? ArtId { get; init; }

    [JsonPropertyName("artist")]
    public string Artist { get; init; }

    [JsonPropertyName("album_release_date")]
    public DateTime? ReleaseDate { get; set; }

    [JsonPropertyName("trackinfo")]
    public List<JsonTrack> Tracks { get; init; }

    /// <summary>
    /// Converts the parsed JSON to an <see cref="Album" />.
    /// </summary>
    /// <param name="bandId">
    /// The Bandcamp id of the band, extracted from the HTML page. It is needed to query the Bandcamp
    /// API and complete the tracks whose MP3 URL is missing from the page data.
    /// </param>
    public Album ToAlbum(long? bandId = null)
    {
        // Some albums do not have a cover art
        var artworkUrl = ArtId == null ? null : URL_START + ArtId.ToString().PadLeft(10, '0') + URL_END;

        // Albums might not have an "album_release_date", especially when they're composed of a single track
        var releaseDate = ReleaseDate ?? AlbumData.ReleaseDate ?? AlbumData.PublishDate ?? new DateTime();

        var album = new Album(Artist, artworkUrl, releaseDate, AlbumData.AlbumTitle)
        {
            BandId = bandId,
            TralbumId = Id,
            TralbumType = AlbumData.Type,
        };

        // Keep the tracks that have no MP3 URL instead of silently dropping them: the downloader will
        // try to complete them from the Bandcamp API, and will skip them with a clear warning if
        // Bandcamp does not expose a stream for them (purchase-only tracks, unreleased tracks, ...).
        album.Tracks = (Tracks ?? []).Select(track => track.ToTrack(album)).ToList();

        return album;
    }
}
