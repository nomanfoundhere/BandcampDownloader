using Avalonia;
using Avalonia.Styling;
using BandcampDownloader.Settings;

namespace BandcampDownloader.Avalonia.Core.Theme;

internal interface IThemeService
{
    /// <summary>
    /// Applies the specified <see cref="Skin" /> to the application.
    /// </summary>
    void ApplySkin(Skin skin);
}

/// <summary>
/// Maps the application <see cref="Skin" /> to the Avalonia Fluent theme variant.
/// </summary>
internal sealed class AvaloniaThemeService : IThemeService
{
    public void ApplySkin(Skin skin)
    {
        if (Application.Current == null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = skin switch
        {
            Skin.Dark => ThemeVariant.Dark,
            Skin.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
    }
}
