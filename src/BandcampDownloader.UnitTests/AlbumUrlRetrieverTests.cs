using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BandcampDownloader.Bandcamp.Download;
using BandcampDownloader.Bandcamp.Extraction;
using BandcampDownloader.Net;
using Moq;

namespace BandcampDownloader.UnitTests;

public sealed class AlbumUrlRetrieverTests
{
    private Mock<IDiscographyService> _discographyService;
    private Mock<IHttpService> _httpService;
    private AlbumUrlRetriever _sut;

    [SetUp]
    public void Setup()
    {
        _discographyService = new Mock<IDiscographyService>();
        _httpService = new Mock<IHttpService>();
        _sut = new AlbumUrlRetriever(_discographyService.Object, _httpService.Object);
    }

    [Test]
    public async Task RetrieveAlbumsUrlsAsync_Splits_Urls_Separated_By_CrLf()
    {
        // Arrange: URLs pasted from Windows (or from a web form) are separated by "\r\n".
        const string inputUrls = "https://artist1.bandcamp.com/album/album1\r\nhttps://artist2.bandcamp.com/album/album2";

        // Act
        var urls = await _sut.RetrieveAlbumsUrlsAsync(inputUrls, downloadArtistDiscography: false, CancellationToken.None);

        // Assert
        Assert.That(urls, Is.EqualTo(new[]
        {
            "https://artist1.bandcamp.com/album/album1",
            "https://artist2.bandcamp.com/album/album2",
        }));
    }

    [Test]
    public async Task RetrieveAlbumsUrlsAsync_Splits_Urls_Separated_By_Lf()
    {
        // Arrange: URLs pasted from macOS/Linux are separated by "\n".
        const string inputUrls = "https://artist1.bandcamp.com/album/album1\nhttps://artist2.bandcamp.com/album/album2";

        // Act
        var urls = await _sut.RetrieveAlbumsUrlsAsync(inputUrls, downloadArtistDiscography: false, CancellationToken.None);

        // Assert
        Assert.That(urls, Is.EqualTo(new[]
        {
            "https://artist1.bandcamp.com/album/album1",
            "https://artist2.bandcamp.com/album/album2",
        }));
    }

    [Test]
    public async Task RetrieveAlbumsUrlsAsync_Splits_Mixed_Separators()
    {
        // Arrange
        const string inputUrls =
            "https://artist1.bandcamp.com/album/album1\r\n" +
            "https://artist2.bandcamp.com/album/album2\n" +
            "https://artist3.bandcamp.com/album/album3\r\n" +
            "https://artist4.bandcamp.com/album/album4";

        // Act
        var urls = await _sut.RetrieveAlbumsUrlsAsync(inputUrls, downloadArtistDiscography: false, CancellationToken.None);

        // Assert
        Assert.That(urls, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task RetrieveAlbumsUrlsAsync_Trims_Urls_And_Removes_Empty_Lines()
    {
        // Arrange: blank lines and surrounding whitespace are common when copy/pasting a list of URLs.
        const string inputUrls =
            "  https://artist1.bandcamp.com/album/album1  \r\n" +
            "\r\n" +
            "\n" +
            "  https://artist2.bandcamp.com/album/album2\n" +
            "\r\n";

        // Act
        var urls = await _sut.RetrieveAlbumsUrlsAsync(inputUrls, downloadArtistDiscography: false, CancellationToken.None);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(urls, Has.Count.EqualTo(2));
            Assert.That(urls, Does.Contain("https://artist1.bandcamp.com/album/album1"));
            Assert.That(urls, Does.Contain("https://artist2.bandcamp.com/album/album2"));
            Assert.That(urls.All(url => url == url.Trim()), Is.True, "Returned URLs must be trimmed");
        }
    }

    [Test]
    public async Task RetrieveAlbumsUrlsAsync_Removes_Duplicate_Urls()
    {
        // Arrange
        const string inputUrls =
            "https://artist1.bandcamp.com/album/album1\n" +
            "https://artist1.bandcamp.com/album/album1\n" +
            "https://artist2.bandcamp.com/album/album2\n";

        // Act
        var urls = await _sut.RetrieveAlbumsUrlsAsync(inputUrls, downloadArtistDiscography: false, CancellationToken.None);

        // Assert
        Assert.That(urls, Is.EqualTo(new[]
        {
            "https://artist1.bandcamp.com/album/album1",
            "https://artist2.bandcamp.com/album/album2",
        }));
    }

    [Test]
    public async Task RetrieveAlbumsUrlsAsync_Trims_Before_Deduplicating()
    {
        // Arrange: the same URL with different surrounding whitespace must be downloaded only once.
        const string inputUrls =
            "https://artist.bandcamp.com/album/x  \r\n" +
            " https://artist.bandcamp.com/album/x \n" +
            "https://artist.bandcamp.com/album/y";

        // Act
        var urls = await _sut.RetrieveAlbumsUrlsAsync(inputUrls, downloadArtistDiscography: false, CancellationToken.None);

        // Assert
        Assert.That(urls, Is.EqualTo(new[]
        {
            "https://artist.bandcamp.com/album/x",
            "https://artist.bandcamp.com/album/y",
        }));
    }

    [Test]
    public async Task RetrieveAlbumsUrlsAsync_Drops_WhitespaceOnly_Entries()
    {
        // Arrange: blank lines are removed by the split; lines made only of spaces must be dropped after trim.
        const string inputUrls = "https://artist.bandcamp.com/album/x\n   \n\t\nhttps://artist.bandcamp.com/album/y";

        // Act
        var urls = await _sut.RetrieveAlbumsUrlsAsync(inputUrls, downloadArtistDiscography: false, CancellationToken.None);

        // Assert
        Assert.That(urls, Is.EqualTo(new[]
        {
            "https://artist.bandcamp.com/album/x",
            "https://artist.bandcamp.com/album/y",
        }));
    }

    [Test]
    public async Task RetrieveAlbumsUrlsAsync_Does_Not_Use_Network_When_Discography_Is_Disabled()
    {
        // Arrange
        const string inputUrls = "https://artist1.bandcamp.com/album/album1";

        // Act
        await _sut.RetrieveAlbumsUrlsAsync(inputUrls, downloadArtistDiscography: false, CancellationToken.None);

        // Assert: no HTTP client is ever created when the user does not request the artist discography.
        _httpService.Verify(x => x.CreateHttpClient(), Times.Never);
        _discographyService.Verify(x => x.GetReferredAlbumsRelativeUrls(It.IsAny<string>()), Times.Never);
    }
}
