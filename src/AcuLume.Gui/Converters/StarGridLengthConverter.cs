using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace AcuLume.Gui.Converters;

/// <summary>
/// Turns a 0..1 fraction into a star <see cref="GridLength"/> so the split-view divider position can
/// be driven by a plain double on the view model. Pass "invert" to get the complementary share.
/// </summary>
public sealed class StarGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value is double d ? Math.Clamp(d, 0, 1) : 0.5;
        if (parameter is "invert")
        {
            fraction = 1 - fraction;
        }

        return new GridLength(fraction, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
