using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using BandcampDownloader.Avalonia.Core.Localization;

namespace BandcampDownloader.Avalonia.UI.Shared;

/// <summary>
/// Represents one value of an enumeration, with its localized description, so that it can be displayed
/// in a ComboBox.
/// </summary>
internal sealed class EnumOption<T> where T : struct, Enum
{
    private static readonly IReadOnlyList<EnumOption<T>> AllOptions = BuildAllOptions();

    public EnumOption(T value)
    {
        Value = value;
        Description = GetDescription(value);
    }

    public T Value { get; }

    public string Description { get; }

    /// <summary>
    /// Returns every value of the enumeration.
    /// </summary>
    public static IReadOnlyList<EnumOption<T>> All() => AllOptions;

    /// <summary>
    /// Returns the option matching the specified value.
    /// </summary>
    public static EnumOption<T> FromValue(T value)
    {
        return AllOptions.FirstOrDefault(o => EqualityComparer<T>.Default.Equals(o.Value, value)) ?? new EnumOption<T>(value);
    }

    public override string ToString() => Description;

    private static IReadOnlyList<EnumOption<T>> BuildAllOptions()
    {
        return Enum.GetValues<T>()
            .Select(value => new EnumOption<T>(value))
            .ToList();
    }

    /// <summary>
    /// Resolution order: localized resource named "{EnumType}_{Value}", then the [Description]
    /// attribute, then the enum value name. This matches what the WPF version displayed.
    /// </summary>
    private static string GetDescription(T value)
    {
        var resourceKey = $"{typeof(T).Name}_{value}";
        var localized = Localizer.Instance[resourceKey];
        if (localized != resourceKey)
        {
            return localized;
        }

        var field = typeof(T).GetField(value.ToString());
        var descriptionAttribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return descriptionAttribute?.Description ?? value.ToString();
    }
}
