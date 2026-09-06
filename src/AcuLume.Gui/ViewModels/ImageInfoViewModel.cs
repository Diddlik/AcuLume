using System.Collections.ObjectModel;
using System.Globalization;
using AcuLume.Core.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AcuLume.Gui.ViewModels;

/// <summary>
/// Read-only view of the image the Sharpen panel currently holds, plus the resize maths that the
/// current output setting implies. Reads nothing the pipeline would not read itself.
/// </summary>
public sealed partial class ImageInfoViewModel(SharpenViewModel sharpen) : ObservableObject
{
    /// <summary>EXIF tags worth showing, in the order a photographer reads them.</summary>
    private static readonly (string Tag, string Label)[] CaptureTags =
    [
        ("Make", "Camera make"),
        ("Model", "Camera model"),
        ("LensModel", "Lens"),
        ("FocalLength", "Focal length"),
        ("FNumber", "Aperture"),
        ("ExposureTime", "Shutter"),
        ("ISOSpeedRatings", "ISO"),
        ("DateTimeOriginal", "Captured"),
    ];

    public ObservableCollection<InfoRow> FileRows { get; } = [];

    public ObservableCollection<InfoRow> CaptureRows { get; } = [];

    public ObservableCollection<InfoRow> OutputRows { get; } = [];

    [ObservableProperty]
    public partial string FileName { get; set; } = "No image loaded";

    [ObservableProperty]
    public partial bool HasImage { get; set; }

    [ObservableProperty]
    public partial string SourceSize { get; set; } = "—";

    [ObservableProperty]
    public partial string OutputSize { get; set; } = "—";

    public void Refresh()
    {
        FileRows.Clear();
        CaptureRows.Clear();
        OutputRows.Clear();

        if (sharpen.ImagePath is not { } path || !File.Exists(path))
        {
            HasImage = false;
            FileName = "No image loaded";
            SourceSize = OutputSize = "—";
            return;
        }

        ImageMetadata metadata;
        try
        {
            metadata = ImageLoader.ReadMetadata(path);
        }
        catch (Exception ex)
        {
            HasImage = false;
            FileName = $"Could not read {Path.GetFileName(path)}: {ex.Message}";
            return;
        }

        HasImage = true;
        FileName = Path.GetFileName(path);

        var file = new FileInfo(path);
        FileRows.Add(new InfoRow("Name", file.Name));
        FileRows.Add(new InfoRow("Folder", file.DirectoryName ?? "—"));
        FileRows.Add(new InfoRow("Size", FormatBytes(file.Length)));
        FileRows.Add(new InfoRow("Modified", file.LastWriteTime.ToString("yyyy-MM-dd HH:mm")));
        FileRows.Add(new InfoRow("Decoder", metadata.Loader));
        FileRows.Add(new InfoRow("Dimensions", $"{metadata.OrientedWidth} × {metadata.OrientedHeight}"));
        FileRows.Add(new InfoRow("Channels", metadata.Bands.ToString()));
        FileRows.Add(new InfoRow("Alpha", metadata.HasAlpha ? "Yes" : "No"));
        FileRows.Add(new InfoRow("EXIF orientation", metadata.Orientation.ToString()));
        FileRows.Add(new InfoRow("ICC profile", metadata.HasIccProfile ? "Embedded" : "None"));

        foreach (var (tag, label) in CaptureTags)
        {
            if (metadata.Exif.TryGetValue(tag, out var value) && value.Length > 0)
            {
                CaptureRows.Add(new InfoRow(label, FormatExif(tag, value)));
            }
        }

        if (CaptureRows.Count == 0)
        {
            CaptureRows.Add(new InfoRow("EXIF", "No capture metadata"));
        }

        BuildOutputRows(metadata);
    }

    private void BuildOutputRows(ImageMetadata metadata)
    {
        var (width, height) = ResizeEngine.CalculateLongEdgeDimensions(
            metadata.OrientedWidth, metadata.OrientedHeight, sharpen.LongEdge, allowUpscale: false);

        SourceSize = $"{metadata.OrientedWidth} × {metadata.OrientedHeight}";
        OutputSize = $"{width} × {height}";

        var scale = width / (double)metadata.OrientedWidth;
        OutputRows.Add(new InfoRow("Scale", $"{scale * 100:0.00}%"));
        OutputRows.Add(new InfoRow("Resize method", scale < 1 ? "Lanczos 3" : "None (no upscale)"));
        OutputRows.Add(new InfoRow("Sharpening space", "Lab L channel"));
        OutputRows.Add(new InfoRow("JPEG quality", sharpen.Quality.ToString()));
        OutputRows.Add(new InfoRow("Metadata", "Preserved (EXIF + ICC)"));
    }

    /// <summary>
    /// EXIF stores exposure values as rationals ("85/1", "22/10"), which libvips passes through
    /// verbatim. Photographers read them as "85 mm" and "f/2.2".
    /// </summary>
    internal static string FormatExif(string tag, string value) => tag switch
    {
        "FocalLength" when Rational(value) is { } mm => $"{mm:0.#} mm",
        "FNumber" when Rational(value) is { } f => $"f/{f:0.#}",
        "ExposureTime" when Rational(value) is { } seconds => seconds >= 1
            ? $"{seconds:0.#} s"
            : $"1/{Math.Round(1 / seconds)} s",
        "DateTimeOriginal" => value.Length >= 10 ? string.Concat(value[..10].Replace(':', '-'), value[10..]) : value,
        _ => value,
    };

    private static double? Rational(string value)
    {
        var slash = value.IndexOf('/');
        if (slash < 0)
        {
            return double.TryParse(value, CultureInfo.InvariantCulture, out var plain) ? plain : null;
        }

        return double.TryParse(value[..slash], CultureInfo.InvariantCulture, out var numerator)
               && double.TryParse(value[(slash + 1)..], CultureInfo.InvariantCulture, out var denominator)
               && denominator != 0
            ? numerator / denominator
            : null;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes} B",
    };
}
