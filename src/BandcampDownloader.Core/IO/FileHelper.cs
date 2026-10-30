using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BandcampDownloader.IO;

internal static class FileHelper
{
    /// <summary>
    /// File systems limit the length of a single path component (255 bytes on most systems) and the
    /// total path length. We keep a safety margin so that the files created by the application are
    /// always valid, on every platform.
    /// </summary>
    private const int MAX_COMPONENT_BYTES = 225;

    /// <summary>
    /// Maximum total path length we generate. macOS has a PATH_MAX of 1024 bytes.
    /// </summary>
    private const int MAX_PATH_LENGTH = 900;

    /// <summary>
    /// Returns the specified file name sanitized so that it can be used on any platform.
    /// </summary>
    public static string ToAllowedFileName(this string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        fileName = fileName.ReplaceInvalidPathCharacters('_');

        // Replace whitespace(s) by a single ' ' (this also removes tabs/newlines) and trim both ends.
        // Trimming must happen before removing the trailing dots: "name...   " must become "name".
        fileName = Regex.Replace(fileName, @"\s+", " ").Trim();

        // Remove trailing dots and whitespace iteratively: a name can end with a mix of both, which a
        // single pass cannot fully clean ("name . ." -> "name . " -> "name ." -> "name").
        while (fileName.Length > 0 && (fileName[^1] == '.' || char.IsWhiteSpace(fileName[^1])))
        {
            fileName = fileName[..^1];
        }

        // A name made only of invalid characters / dots / whitespace would otherwise produce an
        // invalid path (e.g. the album folder itself): fall back to a safe placeholder.
        return string.IsNullOrEmpty(fileName) ? "_" : fileName;
    }

    /// <summary>
    /// Returns the specified path with each of its components shortened if needed, so that it stays
    /// valid on the current platform.
    /// </summary>
    public static string EnsureValidPath(this string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var root = Path.GetPathRoot(path) ?? string.Empty;
        var directorySeparator = Path.DirectorySeparatorChar;
        var segments = path[root.Length..].Split([directorySeparator, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        var builder = new StringBuilder(root);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = ShortenComponent(segments[i]);
            var isLastSegment = i == segments.Length - 1;
            var requiredLength = builder.Length + segment.Length + (isLastSegment ? 0 : 1);

            if (requiredLength > MAX_PATH_LENGTH)
            {
                // The path is already too long: stop here rather than failing to create the file later.
                break;
            }

            if (builder.Length > 0 && builder[^1] != directorySeparator)
            {
                builder.Append(directorySeparator);
            }

            builder.Append(segment);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Shortens the specified path component (file or folder name) if it is too long for the file
    /// system, preserving its extension if any.
    /// </summary>
    private static string ShortenComponent(string component)
    {
        if (Encoding.UTF8.GetByteCount(component) <= MAX_COMPONENT_BYTES)
        {
            return component;
        }

        var extension = Path.GetExtension(component);
        var name = Path.GetFileNameWithoutExtension(component);
        var extensionByteCount = Encoding.UTF8.GetByteCount(extension);

        var allowedNameBytes = Math.Max(1, MAX_COMPONENT_BYTES - extensionByteCount);
        while (name.Length > 0 && Encoding.UTF8.GetByteCount(name) > allowedNameBytes)
        {
            name = name[..^1];
        }

        return name + extension;
    }

    private static string ReplaceInvalidPathCharacters(this string path, char replaceBy)
    {
        foreach (var invalidCharacter in Path.GetInvalidPathChars())
        {
            path = path.Replace(invalidCharacter, replaceBy);
        }

        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            path = path.Replace(invalidCharacter, replaceBy);
        }

        if (!OperatingSystem.IsWindows())
        {
            // ':' is technically allowed by POSIX but is displayed as '/' by the Finder on macOS and is
            // therefore confusing (and can break playlists / tags in other applications).
            path = path.Replace(':', replaceBy);
        }

        return path;
    }
}
