using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BandcampDownloader.Bandcamp.Extraction;
using BandcampDownloader.Net;
using Moq;

namespace BandcampDownloader.UnitTests;

/// <summary>
/// Tests the parsing of the Bandcamp mobile API response used to complete incomplete album pages.
/// </summary>
public sealed class BandcampApiServiceTests
{
    private const string TracksJson = """
        {
          "tracks": [
            {
              "track_id": 111,
              "title": "Full track",
              "track_num": 1,
              "duration": 2669.57,
              "streaming_url": null
            },
            {
              "track_id": 222,
              "title": "Excerpt",
              "track_num": 2,
              "duration": 240.128,
              "streaming_url": {
                "mp3-128": "https://bandcamp.com/stream_redirect?enc=mp3-128&track_id=222"
              }
            }
          ]
        }
        """;

    [Test]
    public async Task GetTracksAsync_Parses_The_Tracks_And_Their_Streaming_Urls()
    {
        // Arrange
        var sut = CreateService(TracksJson);

        // Act
        var tracks = await sut.GetTracksAsync(123, 456, "album", CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tracks, Has.Count.EqualTo(2));

            Assert.That(tracks[0].TrackId, Is.EqualTo(111));
            Assert.That(tracks[0].Title, Is.EqualTo("Full track"));
            Assert.That(tracks[0].Number, Is.EqualTo(1));
            Assert.That(tracks[0].Duration, Is.EqualTo(2669.57));
            Assert.That(tracks[0].Mp3Url, Is.Null, "Bandcamp does not stream that track");

            Assert.That(tracks[1].TrackId, Is.EqualTo(222));
            Assert.That(tracks[1].Title, Is.EqualTo("Excerpt"));
            Assert.That(tracks[1].Number, Is.EqualTo(2));
            Assert.That(tracks[1].Duration, Is.EqualTo(240.128));
            Assert.That(tracks[1].Mp3Url, Is.EqualTo("https://bandcamp.com/stream_redirect?enc=mp3-128&track_id=222"));
        }
    }

    [Test]
    public async Task GetTracksAsync_Requests_The_Expected_Api_Url()
    {
        // Arrange
        Uri? requestedUri = null;
        var sut = CreateService(TracksJson, uri => requestedUri = uri);

        // Act
        await sut.GetTracksAsync(bandId: 123, tralbumId: 456, tralbumType: "album", CancellationToken.None);

        // Assert
        Assert.That(requestedUri, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(requestedUri.Host, Is.EqualTo("bandcamp.com"));
            Assert.That(requestedUri.AbsolutePath, Is.EqualTo("/api/mobile/24/tralbum_details"));
            Assert.That(requestedUri.Query, Does.Contain("band_id=123"));
            Assert.That(requestedUri.Query, Does.Contain("tralbum_type=a"));
            Assert.That(requestedUri.Query, Does.Contain("tralbum_id=456"));
        }
    }

    [Test]
    public async Task GetTracksAsync_Accepts_The_Short_Tralbum_Types()
    {
        // Arrange
        Uri? requestedUri = null;
        var sut = CreateService(TracksJson, uri => requestedUri = uri);

        // Act
        await sut.GetTracksAsync(123, 456, "a", CancellationToken.None);

        // Assert
        Assert.That(requestedUri!.Query, Does.Contain("tralbum_type=a"));
    }

    [Test]
    public async Task GetTracksAsync_Normalizes_Protocol_Relative_Streaming_Urls()
    {
        // Arrange
        const string json = """
            {"tracks":[{"track_id":1,"title":"T","track_num":1,"duration":10,"streaming_url":{"mp3-128":"//example.com/stream/1"}}]}
            """;
        var sut = CreateService(json);

        // Act
        var tracks = await sut.GetTracksAsync(1, 2, "track", CancellationToken.None);

        // Assert
        Assert.That(tracks, Has.Count.EqualTo(1));
        Assert.That(tracks[0].Mp3Url, Is.EqualTo("https://example.com/stream/1"));
    }

    [Test]
    public async Task GetTracksAsync_Returns_An_Empty_List_When_The_Api_Returns_An_Error()
    {
        // Arrange
        var sut = CreateService("""{"error":true,"error_message":"band_id required"}""");

        // Act
        var tracks = await sut.GetTracksAsync(123, 456, "album", CancellationToken.None);

        // Assert
        Assert.That(tracks, Is.Empty);
    }

    [Test]
    public async Task GetTracksAsync_Returns_An_Empty_List_When_The_Http_Call_Fails()
    {
        // Arrange: the response is not valid JSON (or the call throws): the caller must keep the page data.
        var sut = CreateService("<html>Not found</html>");

        // Act
        var tracks = await sut.GetTracksAsync(123, 456, "album", CancellationToken.None);

        // Assert
        Assert.That(tracks, Is.Empty);
    }

    [Test]
    public async Task GetTracksAsync_Returns_An_Empty_List_For_An_Unknown_Tralbum_Type()
    {
        // Arrange: no HTTP call should be made for an unknown type.
        var handlerWasCalled = false;
        var sut = CreateService(TracksJson, _ => handlerWasCalled = true);

        // Act
        var tracks = await sut.GetTracksAsync(123, 456, "mixtape", CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tracks, Is.Empty);
            Assert.That(handlerWasCalled, Is.False);
        }
    }

    private static BandcampApiService CreateService(string json, Action<Uri>? onRequest = null)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri != null)
            {
                onRequest?.Invoke(request.RequestUri);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json),
            };
        }));

        var httpService = new Mock<IHttpService>();
        httpService.Setup(service => service.CreateHttpClient()).Returns(httpClient);

        return new BandcampApiService(httpService.Object);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
