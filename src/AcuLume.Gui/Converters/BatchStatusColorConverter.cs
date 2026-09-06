using System.Globalization;
using AcuLume.Gui.ViewModels;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AcuLume.Gui.Converters;

/// <summary>Status dot colour for a batch queue row.</summary>
public sealed class BatchStatusColorConverter : IValueConverter
{
    private static readonly IBrush Pending = new SolidColorBrush(Color.Parse("#4E565D"));
    private static readonly IBrush Processing = new SolidColorBrush(Color.Parse("#59B0E8"));
    private static readonly IBrush Done = new SolidColorBrush(Color.Parse("#4FBF8B"));
    private static readonly IBrush Failed = new SolidColorBrush(Color.Parse("#E06C6C"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        BatchStatus.Processing => Processing,
        BatchStatus.Done => Done,
        BatchStatus.Failed => Failed,
        _ => Pending,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
