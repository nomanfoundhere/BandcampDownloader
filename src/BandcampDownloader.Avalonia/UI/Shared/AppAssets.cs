using System;
using Avalonia.Platform;
using Avalonia.Controls;

namespace BandcampDownloader.Avalonia.UI.Shared;

/// <summary>
/// Embedded images and icons used by the views.
/// </summary>
internal static class AppAssets
{
    private const string BaseUri = "avares://BandcampDownloader.Avalonia/Assets/";

    public static WindowIcon WindowIcon { get; } = new(AssetLoader.Open(new Uri(BaseUri + "Cloud.png")));

    public static string FromAssets(string fileName) => BaseUri + fileName;
}
