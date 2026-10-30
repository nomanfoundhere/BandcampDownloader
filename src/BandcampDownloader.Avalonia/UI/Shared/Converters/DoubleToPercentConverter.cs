using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace BandcampDownloader.Avalonia.UI.Shared.Converters;

/// <summary>
/// Represents the converter that transforms Double values to and from their percent value.
/// For instance: "0.05 → 5".
/// </summary>
internal sealed class DoubleToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is double d ? d * 100 : 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
        {
            return 0;
        }

        return double.Parse(value.ToString()!, NumberStyles.Any, CultureInfo.InvariantCulture) / 100;
    }
}
