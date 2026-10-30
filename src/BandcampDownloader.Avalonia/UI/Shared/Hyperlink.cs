using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace BandcampDownloader.Avalonia.UI.Shared;

/// <summary>
/// Cross-platform replacement for the WPF hyperlink: a clickable text block that opens the URL in the
/// default browser.
/// </summary>
internal static class Hyperlink
{
    /// <summary>
    /// Opens the specified URL in the default browser.
    /// </summary>
    public static void OpenUrl(string url)
    {
        BandcampDownloader.Helpers.UrlHelper.OpenUrlInBrowser(url);
    }

    /// <summary>
    /// Applies the usual hyperlink look (blue, underlined, hand cursor) to the specified text block.
    /// </summary>
    public static void MakeLookLikeLink(TextBlock textBlock)
    {
        textBlock.Foreground = Brushes.DodgerBlue;
        textBlock.TextDecorations = TextDecorations.Underline;
        textBlock.Cursor = new Cursor(StandardCursorType.Hand);
    }
}
