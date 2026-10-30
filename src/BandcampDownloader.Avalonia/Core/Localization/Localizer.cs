using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Resources;
using System.Text;
using BandcampDownloader.Core.Localization;
using BandcampDownloader.Properties;
using NLog;

namespace BandcampDownloader.Avalonia.Core.Localization;

/// <summary>
/// Provides localized strings to the Avalonia views.
/// Bind to it with the <c>{loc:Loc key}</c> markup extension, or read strings from code with
/// <c>Localizer.Instance["key"]</c>.
/// </summary>
internal sealed class Localizer : INotifyPropertyChanged
{
    private static readonly Lazy<Localizer> LazyInstance = new(() => new Localizer());
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly HashSet<string> _reportedMissingKeys = [];

    public static Localizer Instance => LazyInstance.Value;

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Returns the localized string associated to the specified resource key.
    /// </summary>
    public string this[string key] => GetString(key);

    /// <summary>
    /// Notifies the views that every localized string should be read again.
    /// </summary>
    public void Refresh()
    {
        // Avalonia's bindings to an indexer path ("[key]") listen to the "Item" property name
        // (the WPF "Item[]" convention is NOT understood by Avalonia — verified empirically).
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
    }

    /// <summary>
    /// Subscribes to the language changes so that every localized binding is refreshed automatically.
    /// </summary>
    public void Initialize(ILanguageService languageService)
    {
        languageService.CultureChanged += (_, _) => Refresh();
    }

    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        var value = Resources.ResourceManager.GetString(key, Resources.Culture);
        if (value == null)
        {
            // Report missing keys once; returning the key itself makes the porting mistakes visible in the UI.
            if (_reportedMissingKeys.Add(key))
            {
                _logger.Warn($"Missing localized resource: {key}");
            }

            return key;
        }

        // The resources come from the WPF version, where '_' marks a keyboard mnemonic ("_Download").
        // Avalonia does not use Alt+key mnemonics on macOS and plain TextBlocks would display the
        // underscore literally, so it is stripped from every localized string.
        return StripMnemonicUnderscores(value);
    }

    /// <summary>
    /// Removes the WPF mnemonic underscores from the specified text ("_Download" -> "Download").
    /// A double underscore ("__") is the WPF escape sequence for a literal underscore.
    /// </summary>
    private static string StripMnemonicUnderscores(string text)
    {
        if (text.IndexOf('_') < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character != '_')
            {
                builder.Append(character);
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '_')
            {
                // Escaped literal underscore
                builder.Append('_');
                i++;
            }

            // Otherwise this underscore is a mnemonic marker: drop it.
        }

        return builder.ToString();
    }
}
