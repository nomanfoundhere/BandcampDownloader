using System;
using System.Globalization;
using BandcampDownloader.Settings;
using WPFLocalizeExtension.Engine;

namespace BandcampDownloader.Core.Localization;

/// <summary>
/// WPF implementation of the language service: in addition to updating the current culture (done by
/// the base class), it updates <see cref="LocalizeDictionary" /> so that every localized binding in
/// the XAML views is refreshed.
/// </summary>
internal sealed class WpfLanguageService : LanguageService
{
    public override void ApplyLanguage(Language language)
    {
        base.ApplyLanguage(language);
        LocalizeDictionary.Instance.Culture = CurrentCulture;
    }
}
