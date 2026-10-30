using System;
using System.IO;
using System.Linq;
using System.Text;
using BandcampDownloader.IO;

namespace BandcampDownloader.UnitTests;

public sealed class FileHelperTests
{
    [Test]
    public void ToAllowedFileName_Replaces_InvalidFileNameCharacters()
    {
        // Arrange: build an input containing every character the current platform rejects in a file name.
        var invalidCharacters = Path.GetInvalidFileNameChars();
        Assert.That(invalidCharacters, Is.Not.Empty, "The platform reports no invalid file name character");
        var fileName = "before" + string.Join("", invalidCharacters) + "after";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(invalidCharacters.Any(c => allowedFileName.Contains(c)), Is.False,
                $"'{allowedFileName}' still contains an invalid file name character for this platform");
            Assert.That(allowedFileName, Does.StartWith("before"));
            Assert.That(allowedFileName, Does.EndWith("after"));
        }
    }

    [Test]
    public void ToAllowedFileName_Replaces_PathSeparator()
    {
        // '/' must never end up in a file name (it is the separator of every platform except legacy Windows).
        // Arrange
        const string fileName = "AC/DC - Live";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(allowedFileName, Does.Not.Contain("/"));
            Assert.That(allowedFileName, Is.EqualTo("AC_DC - Live"));
        }
    }

    [Test]
    public void ToAllowedFileName_Replaces_Colon_OnEveryPlatform()
    {
        // ':' is illegal on Windows and is displayed as '/' by the macOS Finder: it must be replaced everywhere.
        // Arrange
        const string fileName = "Album: The Remixes";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("Album_ The Remixes"));
    }

    [Test]
    public void ToAllowedFileName_Removes_TrailingDots()
    {
        // macOS/Windows both trim trailing dots when resolving a path; a name ending with dots would silently
        // lose them (and the extension would no longer match the file format).
        // Arrange
        const string fileName = "Some album...";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("Some album"));
    }

    [Test]
    public void ToAllowedFileName_Removes_TrailingWhitespace()
    {
        // Arrange
        const string fileName = "Some album    ";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("Some album"));
    }

    [Test]
    public void ToAllowedFileName_Trims_Both_Ends()
    {
        // Arrange
        const string fileName = "   Some album   ";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("Some album"));
    }

    [Test]
    public void ToAllowedFileName_Collapses_Whitespace()
    {
        // Arrange
        const string fileName = "Some     album";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("Some album"));
    }

    [Test]
    public void ToAllowedFileName_Collapses_Tabs_And_NewLines_On_NonWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Tabs and new lines are invalid file name characters on Windows: they are replaced by '_' there.");
        }

        // Arrange
        const string fileName = "Some \t\r\n  album";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("Some album"));
    }

    [Test]
    public void ToAllowedFileName_Combines_AllSanitizations()
    {
        // Arrange
        const string fileName = "AC/DC: Live at    the Circus   ";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("AC_DC_ Live at the Circus"));
    }

    [Test]
    public void ToAllowedFileName_Removes_TrailingDots_Even_When_Followed_By_Whitespace()
    {
        // Arrange: dot removal must happen after trimming, otherwise the dots survive.
        const string fileName = "Some album...   ";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("Some album"));
    }

    [Test]
    public void ToAllowedFileName_Removes_Whitespace_Left_By_TrailingDotRemoval()
    {
        // Arrange
        const string fileName = "name . .";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("name"));
    }

    [Test]
    public void ToAllowedFileName_Removes_Only_TrailingDots()
    {
        // Arrange: dots in the middle of the name are legal and must be kept.
        const string fileName = "a...b";

        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("a...b"));
    }

    [TestCase("")]
    [TestCase("...")]
    [TestCase("...   ")]
    public void ToAllowedFileName_Falls_Back_To_Underscore_When_Name_Is_Empty(string fileName)
    {
        // A file/folder name cannot be empty: fall back to a safe placeholder.
        // Act
        var allowedFileName = fileName.ToAllowedFileName();

        // Assert
        Assert.That(allowedFileName, Is.EqualTo("_"));
    }

    [Test]
    public void ToAllowedFileName_Throws_On_Null()
    {
        Assert.Throws<ArgumentNullException>(() => ((string)null!).ToAllowedFileName());
    }

    [Test]
    public void EnsureValidPath_Truncates_LongFileName_And_PreservesExtension()
    {
        // Arrange: 400 ASCII characters plus an extension: far above the per-component limit (255 bytes).
        var fileName = new string('a', 400) + ".mp3";
        var path = Path.Combine(TestEnvironment.DataDirectory, fileName);

        // Act
        var validPath = path.EnsureValidPath();

        // Assert
        var validFileName = Path.GetFileName(validPath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Encoding.UTF8.GetByteCount(validFileName), Is.LessThanOrEqualTo(225));
            Assert.That(Path.GetExtension(validFileName), Is.EqualTo(".mp3"));
            Assert.That(Path.GetFileNameWithoutExtension(validFileName), Is.EqualTo(new string('a', 225 - ".mp3".Length)));
            Assert.That(Path.GetDirectoryName(validPath), Is.EqualTo(TestEnvironment.DataDirectory));
        }
    }

    [Test]
    public void EnsureValidPath_Truncates_LongFileName_WithoutExtension()
    {
        // Arrange
        var fileName = new string('a', 400);
        var path = Path.Combine(TestEnvironment.DataDirectory, fileName);

        // Act
        var validPath = path.EnsureValidPath();

        // Assert
        var validFileName = Path.GetFileName(validPath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Encoding.UTF8.GetByteCount(validFileName), Is.LessThanOrEqualTo(225));
            Assert.That(Path.GetExtension(validFileName), Is.Empty);
        }
    }

    [Test]
    public void EnsureValidPath_Truncates_MultiByteFileName_ByteSafely()
    {
        // Arrange: each 'é' is 2 UTF-8 bytes, so 400 of them are 800 bytes (above the 255-byte file system limit).
        // Cutting the string by characters (not by bytes) must never cut inside a UTF-8 sequence.
        var fileName = new string('é', 400) + ".mp3";
        var path = Path.Combine(TestEnvironment.DataDirectory, fileName);

        // Act
        var validPath = path.EnsureValidPath();

        // Assert
        var validFileName = Path.GetFileName(validPath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Encoding.UTF8.GetByteCount(validFileName), Is.LessThanOrEqualTo(225));
            Assert.That(Path.GetExtension(validFileName), Is.EqualTo(".mp3"));
            Assert.That(validFileName, Does.Match("^é+\\.mp3$"));
            // The result must still be valid Unicode (no lone surrogate / replacement character introduced).
            Assert.That(validFileName.Any(char.IsSurrogate), Is.False);
        }
    }

    [Test]
    public void EnsureValidPath_Truncates_LongDirectoryComponent()
    {
        // Arrange
        var longDirectory = new string('b', 400);
        var path = Path.Combine(TestEnvironment.DataDirectory, longDirectory, "track.mp3");

        // Act
        var validPath = path.EnsureValidPath();

        // Assert
        var directory = Path.GetDirectoryName(validPath)!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Path.GetFileName(validPath), Is.EqualTo("track.mp3"));
            Assert.That(Encoding.UTF8.GetByteCount(Path.GetFileName(directory)), Is.LessThanOrEqualTo(225));
            Assert.That(Path.GetDirectoryName(directory), Is.EqualTo(TestEnvironment.DataDirectory));
        }
    }

    [Test]
    public void EnsureValidPath_Keeps_Path_Under_TotalLengthLimit()
    {
        // Arrange: ten 100-character components: each component is valid, but the whole path exceeds the
        // generated-path limit. The method must drop the trailing components rather than build an invalid path.
        var components = Enumerable.Range(0, 10).Select(_ => new string('c', 100)).ToArray();
        var path = Path.Combine([TestEnvironment.DataDirectory, .. components]);

        // Act
        var validPath = path.EnsureValidPath();

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(validPath.Length, Is.LessThanOrEqualTo(900));
            Assert.That(validPath, Does.StartWith(TestEnvironment.DataDirectory));
            // The returned path is a prefix of the original one (whole components only).
            Assert.That(path, Does.StartWith(validPath));
        }
    }

    [Test]
    public void EnsureValidPath_Uses_Native_Separator_Only()
    {
        // Arrange
        var path = Path.Combine(TestEnvironment.DataDirectory, "artist", "album", "01 track.mp3");

        // Act
        var validPath = path.EnsureValidPath();

        // Assert
        Assert.That(validPath, Is.EqualTo(path));
        if (OperatingSystem.IsWindows())
        {
            Assert.That(validPath.Contains('/'), Is.False, "The path must not contain a forward slash on Windows");
        }
        else
        {
            Assert.That(validPath.Contains('\\'), Is.False, "The path must not contain a backslash on macOS/Linux");
        }
    }

    [Test]
    public void EnsureValidPath_Throws_On_Null()
    {
        Assert.Throws<ArgumentNullException>(() => ((string)null!).EnsureValidPath());
    }
}
