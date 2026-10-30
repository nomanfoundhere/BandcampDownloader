using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BandcampDownloader.Bandcamp.Download;
using BandcampDownloader.Bandcamp.Extraction;
using BandcampDownloader.Core.DependencyInjection;
using BandcampDownloader.Settings;
using TagLib;
using File = System.IO.File;

namespace BandcampDownloader.IntegrationTests;

/// <summary>
/// Live tests against the real bandcamp.com website. They need network access, are tagged with the
/// "Network" category and are <see cref="ExplicitAttribute" /> (so that the regular/CI test runs never
/// hit the network or download anything). Run them explicitly by name:
/// <c>scripts/test.sh src/BandcampDownloader.IntegrationTests/BandcampDownloader.IntegrationTests.csproj --filter "FullyQualifiedName~BandcampLiveTests"</c>.
/// </summary>
[Category("Network")]
public sealed class BandcampLiveTests
{
    private const string AlbumPageUrl = "https://goataholicskjald.bandcamp.com/album/dogma";
    private const string TrackPageUrl = "https://goataholicskjald.bandcamp.com/track/europa";
    private const string ExpectedArtist = "Goataholic";

    // "Europa", the first track of the album, is short (128 s / ~2 MB at 128 kbit/s): download only that one.
    private const string ExpectedTrackTitle = "Europa";

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("BandcampDownloader/1.5.2 (macOS integration test)");
        return httpClient;
    }

    [Test]
    [Explicit("Live network test: run it explicitly with --filter FullyQualifiedName~BandcampLiveTests")]
    [CancelAfter(120_000)]
    public async Task AlbumInfoParser_Extracts_Album_From_Live_AlbumPage()
    {
        // Arrange
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        using var httpClient = CreateHttpClient();
        var htmlContent = await httpClient.GetStringAsync(AlbumPageUrl, cancellationToken);
        TestContext.Out.WriteLine($"[live] downloaded {htmlContent.Length} chars of HTML from {AlbumPageUrl}");

        var sut = new AlbumInfoParser();

        // Act
        var album = sut.GetAlbumInfoFromAlbumPage(htmlContent, cancellationToken);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(album.Title, Is.EqualTo("Dogma"));
            Assert.That(album.Artist, Is.EqualTo(ExpectedArtist));
            Assert.That(album.ReleaseDate.Year, Is.EqualTo(2015));
            Assert.That(album.HasArtwork, Is.True);
            Assert.That(album.Tracks, Has.Count.EqualTo(21), "Dogma has 21 tracks");
            Assert.That(album.Tracks.Select(t => t.Number), Is.EqualTo(Enumerable.Range(1, 21)));
            Assert.That(album.Tracks.All(track => Uri.TryCreate(track.Mp3Url, UriKind.Absolute, out _)), Is.True,
                "Every track must have an absolute mp3 URL");
            Assert.That(album.Tracks.Any(track => track.Title == ExpectedTrackTitle), Is.True);
            Assert.That(album.Tracks.All(track => track.Duration > 0), Is.True);
        }

        TestContext.Out.WriteLine(
            $"[live] parsed album \"{album.Title}\" by \"{album.Artist}\" ({album.ReleaseDate:yyyy-MM-dd}), " +
            $"{album.Tracks.Count} tracks, artwork={album.ArtworkUrl}, first mp3={album.Tracks[0].Mp3Url}");
    }

    [Test]
    [Explicit("Live network test: run it explicitly with --filter FullyQualifiedName~BandcampLiveTests")]
    [CancelAfter(240_000)]
    public async Task DownloadManager_Downloads_SingleTrack_With_Tags_CoverArt_And_Playlist()
    {
        // Arrange: redirect every generated path into the per-run temporary directory.
        var cancellationToken = TestContext.CurrentContext.CancellationToken;
        var downloadsRoot = TestEnvironment.CreateTempDirectory("e2e-downloads");
        var userSettings = TestEnvironment.GetUserSettings();
        userSettings.DownloadsPath = Path.Combine(downloadsRoot, "{artist}", "{album}");
        userSettings.FileNameFormat = "{tracknum} {artist} - {title}.mp3";
        userSettings.DownloadArtistDiscography = false;
        userSettings.ModifyTags = true;
        userSettings.SaveCoverArtInTags = true;
        userSettings.SaveCoverArtInFolder = false;
        userSettings.CreatePlaylist = true;
        userSettings.PlaylistFormat = PlaylistFormat.m3u;
        userSettings.M3uExtended = true;
        userSettings.RetrieveFilesSize = true;
        userSettings.DownloadMaxTries = 3;

        var downloadManager = DependencyInjectionHelper.GetService<IDownloadManager>();
        var progressMessages = new System.Collections.Concurrent.ConcurrentQueue<string>();
        downloadManager.DownloadProgressChanged += (_, args) => progressMessages.Enqueue(args.Message);

        // Act: a /track/ URL contains a single track, so this downloads exactly one track (+ its cover art).
        await downloadManager.InitializeAsync(TrackPageUrl, cancellationToken);
        Assert.That(downloadManager.GetTotalFilesCountToDownload(), Is.EqualTo(2),
            "Expected 2 files to download: the track and its cover art");

        await downloadManager.StartDownloadsAsync(cancellationToken);
        Assert.That(downloadManager.GetTotalFilesCountReceived(), Is.EqualTo(2),
            string.Join(Environment.NewLine, progressMessages));

        // Assert: the mp3 file has been written in a platform-correct path.
        var trackFiles = Directory.GetFiles(downloadsRoot, "*.mp3", SearchOption.AllDirectories);
        Assert.That(trackFiles, Has.Length.EqualTo(1), string.Join(Environment.NewLine, progressMessages));
        var trackPath = trackFiles[0];
        var trackFileInfo = new FileInfo(trackPath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(trackFileInfo.Length, Is.GreaterThan(100_000), "The downloaded mp3 is suspiciously small");
            Assert.That(Path.GetFileName(trackPath), Is.EqualTo($"01 {ExpectedArtist} - {ExpectedTrackTitle}.mp3"));
            Assert.That(trackPath.Contains('\\'), Is.False, "The track path must not contain a Windows separator");
        }

        // Assert: TagLibSharp can read the tags written by the application.
        using var tagFile = TagLib.File.Create(trackPath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tagFile.Tag.Performers, Does.Contain(ExpectedArtist));
            Assert.That(tagFile.Tag.AlbumArtists, Does.Contain(ExpectedArtist));
            Assert.That(tagFile.Tag.Title, Is.EqualTo(ExpectedTrackTitle));
            Assert.That(tagFile.Tag.Album, Is.EqualTo(ExpectedTrackTitle));
            Assert.That(tagFile.Tag.Year, Is.EqualTo(2015U));
            Assert.That(tagFile.Tag.Track, Is.EqualTo(1U));
            Assert.That(tagFile.Tag.Pictures, Is.Not.Empty, "The cover art must be embedded in the tags");
            Assert.That(tagFile.Tag.Pictures[0].Data.Data, Is.Not.Empty);
        }

        // Assert: the m3u playlist references the downloaded file.
        var playlistFiles = Directory.GetFiles(downloadsRoot, "*.m3u", SearchOption.AllDirectories);
        Assert.That(playlistFiles, Has.Length.EqualTo(1));
        var playlistContent = await File.ReadAllTextAsync(playlistFiles[0], cancellationToken);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(playlistContent, Does.Contain("#EXTM3U"));
            Assert.That(playlistContent, Does.Contain(ExpectedTrackTitle));
            Assert.That(playlistContent, Does.Contain(Path.GetFileName(trackPath)));
            Assert.That(playlistContent.Contains('\\'), Is.False);
        }

        TestContext.Out.WriteLine(
            $"[live] downloaded {trackFileInfo.Length} bytes to {trackPath}; " +
            $"tags: artist={string.Join(",", tagFile.Tag.Performers)}, title={tagFile.Tag.Title}, " +
            $"album={tagFile.Tag.Album}, year={tagFile.Tag.Year}, track={tagFile.Tag.Track}, " +
            $"pictures={tagFile.Tag.Pictures.Length}; playlist={playlistFiles[0]} ({new FileInfo(playlistFiles[0]).Length} bytes)");
    }
}
