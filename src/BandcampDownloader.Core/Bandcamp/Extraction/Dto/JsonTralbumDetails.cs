using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BandcampDownloader.Bandcamp.Extraction.Dto;

/// <summary>
/// Response of the Bandcamp mobile API endpoint
/// <c>https://bandcamp.com/api/mobile/24/tralbum_details</c>.
/// </summary>
internal sealed class JsonTralbumDetails
{
    [JsonPropertyName("error")]
    public bool IsError { get; init; }

    [JsonPropertyName("tracks")]
    public List<JsonApiTrack> Tracks { get; init; }
}

internal sealed class JsonApiTrack
{
    [JsonPropertyName("track_id")]
    public long? TrackId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("track_num")]
    public int? TrackNumber { get; init; }

    [JsonPropertyName("duration")]
    public double Duration { get; init; }

    [JsonPropertyName("streaming_url")]
    public JsonMp3File StreamingUrl { get; init; }
}
