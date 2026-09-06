using System.Globalization;
using Avalonia.Data.Converters;

namespace AcuLume.Gui.Converters;

/// <summary>
/// Multiplies a 0..1 fraction by the converter parameter, so a histogram bucket can drive a bar's
/// pixel height without a code-behind drawing pass.
/// </summary>
public sealed class ScaleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var fraction = value is double d ? d : 0;
        var scale = parameter is not null && double.TryParse(
            parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 1;

        // Keep a visible sliver for non-empty buckets so a sparse histogram still reads as a shape.
        return fraction <= 0 ? 0d : Math.Max(1, fraction * scale);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
