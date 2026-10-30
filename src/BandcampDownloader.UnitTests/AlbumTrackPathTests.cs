using System;
using System.IO;
using System.Linq;
using BandcampDownloader.Model;
using BandcampDownloader.Settings;

namespace BandcampDownloader.UnitTests;

/// <summary>
/// Tests the composition of the album/track/playlist/artwork paths from the user settings.
/// These paths are built by the Core on every platform: they must use the platform separators
/// (<see cref="Path.DirectorySeparatorChar" />) and never hard-code Windows backslashes.
/// </summary>
public sealed class AlbumTrackPathTests
{
    private const string Artist = "Goataholic";
    private const string AlbumTitle = "Dogma";
    private static readonly DateTime ReleaseDate = new(2015, 9, 14);

    private IUserSettings _userSettings;
    private string _downloadsRoot;

    [SetUp]
    public void Setup()
    {
        _userSettings = TestEnvironment.GetUserSettings();
        _downloadsRoot = TestEnvironment.CreateTempDirectory("downloads");

        // Reset the settings that influence the generated paths to their defaults.
        _userSettings.DownloadsPath = Path.Combine(_downloadsRoot, "{artist}", "{album}");
        _userSettings.FileNameFormat = "{tracknum} {artist} - {title}.mp3";
        _userSettings.CoverArtFileNameFormat = "{album}";
        _userSettings.PlaylistFileNameFormat = "{album}";
        _userSettings.PlaylistFormat = PlaylistFormat.m3u;
    }

    [Test]
    public void Album_Path_Replaces_Placeholders_With_NativeSeparators()
    {
        // Arrange
        _userSettings.DownloadsPath = Path.Combine(_downloadsRoot, "{year}", "{artist}", "{album}");

        // Act
        var album = CreateAlbum();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(album.Path, Is.EqualTo(Path.Combine(_downloadsRoot, "2015", Artist, AlbumTitle)));
            AssertNoForeignSeparator(album.Path);
            Assert.That(Path.IsPathRooted(album.Path), Is.True);
        }
    }

    [Test]
    public void Album_Path_Replaces_All_Date_Placeholders()
    {
        // Arrange
        _userSettings.DownloadsPath = Path.Combine(_downloadsRoot, "{year}", "{month}", "{day}", "{album}");

        // Act
        var album = CreateAlbum();

        // Assert
        Assert.That(album.Path, Is.EqualTo(Path.Combine(_downloadsRoot, "2015", "09", "14", AlbumTitle)));
    }

    [Test]
    public void Album_Path_Sanitizes_InvalidCharacters_Of_Artist_And_Title()
    {
        // Arrange
        _userSettings.DownloadsPath = Path.Combine(_downloadsRoot, "{artist}", "{album}");
        var artist = "AC/DC";
        var title = "Live: at the Circus";

        // Act
        var album = new Album(artist, null, ReleaseDate, title);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Path.GetFileName(album.Path), Does.Not.Contain("/"));
            AssertNoForeignSeparator(album.Path);
            // The Finder displays ':' as '/': it must have been replaced with '_'.
            Assert.That(Path.GetFileName(album.Path), Is.EqualTo("Live_ at the Circus"));
        }
    }

    [Test]
    public void Album_ArtworkPath_Uses_Extension_From_Url_Ignoring_QueryString()
    {
        // Arrange: Bandcamp artwork URLs may carry a query string; only the URL path holds the extension.
        const string artworkUrl = "https://f4.bcbits.com/img/a2891495342_10.jpg?w=280&h=280";

        // Act
        var album = CreateAlbum(artworkUrl);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(album.HasArtwork, Is.True);
            Assert.That(album.ArtworkPath, Is.Not.Null);
            Assert.That(album.ArtworkPath!, Is.EqualTo(Path.Combine(album.Path, AlbumTitle + ".jpg")));
            Assert.That(album.ArtworkPath!.Contains('?'), Is.False);
        }
    }

    [Test]
    public void Album_ArtworkPath_Uses_Extension_From_Url_Without_QueryString()
    {
        // Arrange
        const string artworkUrl = "https://f4.bcbits.com/img/a2891495342_10.png";

        // Act
        var album = CreateAlbum(artworkUrl);

        // Assert
        Assert.That(album.ArtworkPath, Is.EqualTo(Path.Combine(album.Path, AlbumTitle + ".png")));
    }

    [Test]
    public void Album_ArtworkPath_FallsBack_To_Jpg_When_Url_Has_No_Extension()
    {
        // Arrange
        const string artworkUrl = "https://example.com/artwork?id=2891495342";

        // Act
        var album = CreateAlbum(artworkUrl);

        // Assert
        Assert.That(album.ArtworkPath, Is.EqualTo(Path.Combine(album.Path, AlbumTitle + ".jpg")));
    }

    [Test]
    public void Album_Without_Artwork_Has_No_ArtworkPath()
    {
        // Arrange / Act
        var album = CreateAlbum(artworkUrl: null);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(album.HasArtwork, Is.False);
            Assert.That(album.ArtworkPath, Is.Null);
        }
    }

    [Test]
    public void Album_PlaylistPath_Uses_PlaylistFormat_And_NativeSeparators()
    {
        // Arrange
        var album = CreateAlbum();

        // Act
        var playlistPath = album.PlaylistPath;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(playlistPath, Is.EqualTo(Path.Combine(album.Path, AlbumTitle + ".m3u")));
            AssertNoForeignSeparator(playlistPath);
        }
    }

    [Test]
    public void Album_PlaylistPath_Uses_Pls_Extension_When_Configured()
    {
        // Arrange
        _userSettings.PlaylistFormat = PlaylistFormat.pls;

        // Act
        var album = CreateAlbum();

        // Assert
        Assert.That(Path.GetExtension(album.PlaylistPath), Is.EqualTo(".pls"));
    }

    [Test]
    public void Album_PlaylistPath_Sanitizes_PlaylistFileName()
    {
        // Arrange: a ':' is displayed as '/' on macOS and '/' is a path separator on every platform.
        _userSettings.PlaylistFileNameFormat = "{artist}: {album}";
        var album = new Album("AC/DC", null, ReleaseDate, "Live");

        // Act
        var playlistFileName = Path.GetFileName(album.PlaylistPath);

        // Assert
        Assert.That(playlistFileName, Is.EqualTo("AC_DC_ Live.m3u"));
    }

    [Test]
    public void Track_Path_Replaces_Placeholders_And_Is_Inside_AlbumPath()
    {
        // Arrange
        var album = CreateAlbum();
        var track = new Track(album, 128.81, null, "https://t4.bcbits.com/stream/123/mp3-128/122488544", 1, "Europa");

        // Act
        var trackPath = track.Path;

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(trackPath, Is.EqualTo(Path.Combine(album.Path, "01 Goataholic - Europa.mp3")));
            Assert.That(Path.GetDirectoryName(trackPath), Is.EqualTo(album.Path));
            AssertNoForeignSeparator(trackPath);
            Assert.That(Path.GetExtension(trackPath), Is.EqualTo(".mp3"));
        }
    }

    [Test]
    public void Track_Path_Uses_TwoDigit_TrackNumber()
    {
        // Arrange
        var album = CreateAlbum();

        // Act
        var track = new Track(album, 100, null, "https://example.com/track.mp3", 9, "Old tune");

        // Assert
        Assert.That(Path.GetFileName(track.Path), Is.EqualTo("09 Goataholic - Old tune.mp3"));
    }

    [Test]
    public void Track_Path_Sanitizes_InvalidCharacters()
    {
        // Arrange
        var album = CreateAlbum();
        var track = new Track(album, 100, null, "https://example.com/track.mp3", 2, "AC/DC: Live?");

        // Act
        var fileName = Path.GetFileName(track.Path);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(fileName, Does.Not.Contain("/"));
            Assert.That(fileName, Does.Not.Contain(":"));
            Assert.That(Path.GetInvalidFileNameChars().Any(c => fileName.Contains(c)), Is.False);
        }
    }

    [Test]
    public void Track_Path_Uses_FileNameFormat_From_Settings()
    {
        // Arrange
        _userSettings.FileNameFormat = "{artist} - {album} - {tracknum} - {title}.mp3";
        var album = CreateAlbum();

        // Act
        var track = new Track(album, 100, null, "https://example.com/track.mp3", 3, "Feats of Strength");

        // Assert
        Assert.That(Path.GetFileName(track.Path), Is.EqualTo("Goataholic - Dogma - 03 - Feats of Strength.mp3"));
    }

    /// <summary>
    /// Asserts that the path only uses the separators of the current platform: the Core must build its
    /// paths with <see cref="Path.Combine" />, never with a hard-coded Windows or Unix separator.
    /// </summary>
    private static void AssertNoForeignSeparator(string path)
    {
        var foreignSeparator = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
        Assert.That(
            path.Contains(foreignSeparator),
            Is.False,
            $"'{path}' must not contain the foreign separator '{foreignSeparator}' on this platform");
    }

    private static Album CreateAlbum(string? artworkUrl = "https://f4.bcbits.com/img/a2891495342_10.jpg")
    {
        return new Album(Artist, artworkUrl, ReleaseDate, AlbumTitle);
    }
}
