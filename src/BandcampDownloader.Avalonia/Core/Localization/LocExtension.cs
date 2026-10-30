using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace BandcampDownloader.Avalonia.Core.Localization;

/// <summary>
/// Markup extension used to bind a localized resource: <c>{loc:Loc buttonStart}</c>.
/// The binding is refreshed automatically when the application language changes.
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// The resource key (see the "Resources.resx" file of the Core project).
    /// </summary>
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
        {
            // Always pass the resource key explicitly: <c>Content="{loc:Loc buttonStart}"</c>.
            return string.Empty;
        }

        return new Binding
        {
            Source = Localizer.Instance,
            Path = $"[{Key}]",
            Mode = BindingMode.OneWay,
        };
    }
}
