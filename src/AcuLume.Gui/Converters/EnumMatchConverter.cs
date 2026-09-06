using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace AcuLume.Gui.Converters;

/// <summary>
/// Two-way match between a property's value and a RadioButton's IsChecked, so segmented controls in
/// the panel bind straight to the view model without a bool property per segment. Handles enums and
/// integers — the compare view's column count is a plain number.
/// </summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public static EnumMatchConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && value.ToString() == parameter.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null)
        {
            return BindingOperations.DoNothing;
        }

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var text = parameter.ToString()!;
        return type.IsEnum ? Enum.Parse(type, text) : System.Convert.ChangeType(text, type, culture);
    }
}
