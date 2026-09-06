using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AcuLume.Gui.Converters;

/// <summary>Parses an SVG path string into a Geometry, so icon data can live in the view model.</summary>
public sealed class PathGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string data && data.Length > 0 ? Geometry.Parse(data) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
