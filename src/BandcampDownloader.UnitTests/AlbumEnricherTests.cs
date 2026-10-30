using System;
using System.Collections.Generic;
using System.Linq;
using BandcampDownloader.Bandcamp.Extraction;
using BandcampDownloader.Model;

namespace BandcampDownloader.UnitTests;

/// <summary>
/// Tests the merge of the tracks parsed from an HTML page with the tracks returned by the Bandcamp
/// API. This is what allows downloading albums whose page data is incomplete (for instance when a
/// purchase-only full track has no stream URL while a public excerpt is streamable).
/// </summary>
public sealed class AlbumEnricherTests
{
    private Album _album;

    [SetUp]
    public void Setup()
    {
        TestEnvironment.GetUserSettings(); // The Album/Track models resolve the settings from the DI container.

        _album = new Album("Artist", "https://f4.bcbits.com/img/a0000000001_0.jpg", new DateTime(2022, 1, 1), "Album")
        {
            Tracks = [],
        };
    }

    [Test]
    public void Enrich_Fills_The_Missing_Mp3_Url_Of_An_Existing_Track()
    {
        // Arrange: the page data mentions the track but has no MP3 URL for it.
        _album.Tracks.Add(new Track(_album, 300, null, null, 1, "Full track") { TrackId = 111 });

        // Act
        var enrichedCount = AlbumEnricher.Enrich(_album,
        [
            new BandcampApiTrack { TrackId = 111, Title = "Full track", Number = 1, Duration = 300, Mp3Url = "https://example.com/stream/111" },
        ]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enrichedCount, Is.EqualTo(1));
            Assert.That(_album.Tracks, Has.Count.EqualTo(1));
            Assert.That(_album.Tracks[0].Mp3Url, Is.EqualTo("https://example.com/stream/111"));
        }
    }

    [Test]
    public void Enrich_Adds_A_Streamable_Track_That_Is_Missing_From_The_Page_Data()
    {
        // Arrange: only the purchase-only track is in the page data; the API knows about a public excerpt.
        _album.Tracks.Add(new Track(_album, 2669, null, null, 1, "Full track") { TrackId = 111 });

        // Act
        var enrichedCount = AlbumEnricher.Enrich(_album,
        [
            new BandcampApiTrack { TrackId = 111, Title = "Full track", Number = 1, Duration = 2669, Mp3Url = null },
            new BandcampApiTrack { TrackId = 222, Title = "Excerpt", Number = 2, Duration = 240, Mp3Url = "https://example.com/stream/222" },
        ]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enrichedCount, Is.EqualTo(1));
            Assert.That(_album.Tracks, Has.Count.EqualTo(2));
            Assert.That(_album.Tracks[1].TrackId, Is.EqualTo(222));
            Assert.That(_album.Tracks[1].Title, Is.EqualTo("Excerpt"));
            Assert.That(_album.Tracks[1].Number, Is.EqualTo(2));
            Assert.That(_album.Tracks[1].Mp3Url, Is.EqualTo("https://example.com/stream/222"));
            Assert.That(_album.Tracks[1].TrackId, Is.EqualTo(222));
        }
    }

    [Test]
    public void Enrich_Ignores_Tracks_That_Bandcamp_Does_Not_Stream()
    {
        // Arrange
        _album.Tracks.Add(new Track(_album, 2669, null, null, 1, "Full track") { TrackId = 111 });

        // Act
        var enrichedCount = AlbumEnricher.Enrich(_album,
        [
            new BandcampApiTrack { TrackId = 111, Title = "Full track", Number = 1, Duration = 2669, Mp3Url = null },
        ]);

        // Assert: the track is kept (so that the caller can skip it with a warning) but not completed.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enrichedCount, Is.Zero);
            Assert.That(_album.Tracks, Has.Count.EqualTo(1));
            Assert.That(_album.Tracks[0].Mp3Url, Is.Null);
        }
    }

    [Test]
    public void Enrich_Does_Not_Overwrite_An_Existing_Mp3_Url()
    {
        // Arrange: the HTML URL is the one used when available (same stream, but keep current behaviour).
        _album.Tracks.Add(new Track(_album, 100, null, "https://example.com/stream/html", 1, "One") { TrackId = 111 });

        // Act
        var enrichedCount = AlbumEnricher.Enrich(_album,
        [
            new BandcampApiTrack { TrackId = 111, Title = "One", Number = 1, Duration = 100, Mp3Url = "https://example.com/stream/api" },
        ]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enrichedCount, Is.Zero);
            Assert.That(_album.Tracks[0].Mp3Url, Is.EqualTo("https://example.com/stream/html"));
        }
    }

    [Test]
    public void Enrich_Keeps_The_Album_Tracks_Sorted_By_Number()
    {
        // Arrange: a streamable bonus track in the middle of the album is missing from the page data.
        _album.Tracks.Add(new Track(_album, 100, null, "https://example.com/stream/111", 1, "One") { TrackId = 111 });
        _album.Tracks.Add(new Track(_album, 100, null, "https://example.com/stream/333", 3, "Three") { TrackId = 333 });

        // Act
        var enrichedCount = AlbumEnricher.Enrich(_album,
        [
            new BandcampApiTrack { TrackId = 222, Title = "Two", Number = 2, Duration = 100, Mp3Url = "https://example.com/stream/222" },
        ]);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enrichedCount, Is.EqualTo(1));
            Assert.That(_album.Tracks.Select(track => track.Number), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(_album.Tracks[1].Title, Is.EqualTo("Two"));
        }
    }

    [Test]
    public void Enrich_Does_Nothing_When_The_Api_Returns_No_Track()
    {
        // Arrange
        _album.Tracks.Add(new Track(_album, 100, null, null, 1, "One") { TrackId = 111 });

        // Act
        var enrichedCount = AlbumEnricher.Enrich(_album, Array.Empty<BandcampApiTrack>());

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enrichedCount, Is.Zero);
            Assert.That(_album.Tracks, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Enrich_Adds_Tracks_When_The_Page_Had_No_Track_At_All()
    {
        // Arrange: a page whose data-tralbum contains no usable track (e.g. all tracks are purchase-only).
        var apiTracks = new List<BandcampApiTrack>
        {
            new() { TrackId = 222, Title = "Excerpt", Number = 1, Duration = 240, Mp3Url = "https://example.com/stream/222" },
        };

        // Act
        var enrichedCount = AlbumEnricher.Enrich(_album, apiTracks);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(enrichedCount, Is.EqualTo(1));
            Assert.That(_album.Tracks, Has.Count.EqualTo(1));
            Assert.That(_album.Tracks[0].Number, Is.EqualTo(1));
        }
    }
}
