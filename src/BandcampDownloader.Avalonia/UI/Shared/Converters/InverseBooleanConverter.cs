using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace BandcampDownloader.Avalonia.UI.Shared.Converters;

/// <summary>
/// Converts Boolean values to and from their inverse Boolean values.
/// </summary>
internal sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(value is bool b && b);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !(value is bool b && b);
    }
}
