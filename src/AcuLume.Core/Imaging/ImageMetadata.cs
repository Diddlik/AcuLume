namespace AcuLume.Core.Imaging;

/// <summary>Read-only summary of an image file, used by the CLI `info` command and the GUI's info view.</summary>
public sealed record ImageMetadata
{
    public required string Path { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Bands { get; init; }
    public required bool HasAlpha { get; init; }
    public required string Loader { get; init; }
    public required int Orientation { get; init; }
    public required bool HasIccProfile { get; init; }

    /// <summary>
    /// Capture metadata read from EXIF, keyed by tag name (Make, Model, FNumber, ...). Empty when the
    /// file carries no EXIF. Values are the human-readable form libvips reports, with its trailing
    /// type annotation stripped.
    /// </summary>
    public IReadOnlyDictionary<string, string> Exif { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Orientations 5-8 swap width and height once EXIF rotation is baked into the pixels.</summary>
    public bool IsRotated => Orientation is >= 5 and <= 8;

    public int OrientedWidth => IsRotated ? Height : Width;

    public int OrientedHeight => IsRotated ? Width : Height;
}
