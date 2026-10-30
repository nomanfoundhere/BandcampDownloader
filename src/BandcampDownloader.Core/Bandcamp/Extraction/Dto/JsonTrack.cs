using System;
using System.Text.Json.Serialization;
using BandcampDownloader.Model;

namespace BandcampDownloader.Bandcamp.Extraction.Dto;

internal sealed class JsonTrack
{
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("file")]
    public JsonMp3File File { get; set; }

    [JsonPropertyName("lyrics")]
    public string Lyrics { get; set; }

    [JsonPropertyName("track_num")]
    public int? Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    public Track ToTrack(Album album)
    {
        // The MP3 URL is not always present in the page data: purchase-only tracks and unreleased
        // tracks have no "file" entry. Keep the track anyway (with a null URL) so that the Bandcamp
        // API fallback can complete it, or so that it can be skipped with a clear warning.
        var fileUrl = File?.Url;
        var mp3Url = string.IsNullOrWhiteSpace(fileUrl)
            ? null
            : (fileUrl.StartsWith("//", StringComparison.Ordinal) ? "https:" + fileUrl : fileUrl); // "//example.com" Uri lacks protocol
        var number = Number ?? 1; // For bandcamp track pages, Number will be null. Set 1 instead

        return new Track(album, Duration, Lyrics, mp3Url, number, Title)
        {
            TrackId = Id ?? 0,
        };
    }
}
