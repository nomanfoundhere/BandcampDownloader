using System;
using System.Globalization;
using BandcampDownloader.Properties;
using BandcampDownloader.Settings;

namespace BandcampDownloader.Core.Localization;

internal interface ILanguageService
{
    /// <summary>
    /// Raised when the language of the application changes.
    /// </summary>
    event EventHandler CultureChanged;

    /// <summary>
    /// The culture currently used by the application.
    /// </summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// Applies the specified language.
    /// </summary>
    void ApplyLanguage(Language language);
}

/// <summary>
/// Platform-independent language service. It updates the current culture and the localized resources
/// so that strings looked up from code (message boxes, ...) are localized too.
/// </summary>
internal class LanguageService : ILanguageService
{
    public event EventHandler CultureChanged;

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentUICulture;

    public virtual void ApplyLanguage(Language language)
    {
        var culture = GetCultureInfo(language);

        CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Resources.Culture = culture;

        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the CultureInfo corresponding to the specified Language.
    /// </summary>
    /// <param name="language">The Language.</param>
    public static CultureInfo GetCultureInfo(Language language)
    {
        // Existing cultures: https://dotnetfiddle.net/e1BX7M
        return language switch
        {
            Language.ar => new CultureInfo("ar"),
            Language.ca => new CultureInfo("ca"),
            Language.de => new CultureInfo("de"),
            Language.en => new CultureInfo("en"),
            // Language.eo => new CultureInfo("eo"),
            Language.es => new CultureInfo("es"),
            Language.fi => new CultureInfo("fi"),
            Language.fr => new CultureInfo("fr"),
            Language.hr => new CultureInfo("hr"),
            Language.hu => new CultureInfo("hu"),
            Language.id => new CultureInfo("id"),
            Language.it => new CultureInfo("it"),
            // Language.ja => new CultureInfo("ja"),
            // Language.ko => new CultureInfo("ko"),
            Language.nb_NO => new CultureInfo("nb-NO"),
            Language.pl => new CultureInfo("pl"),
            Language.nl => new CultureInfo("nl"),
            Language.pt => new CultureInfo("pt"),
            Language.pt_BR => new CultureInfo("pt-BR"),
            Language.ru => new CultureInfo("ru"),
            Language.sv => new CultureInfo("sv"),
            Language.tr => new CultureInfo("tr"),
            Language.uk => new CultureInfo("uk"),
            Language.vi => new CultureInfo("vi"),
            Language.zh => new CultureInfo("zh"),
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };
    }
}
