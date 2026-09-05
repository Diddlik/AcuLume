namespace AcuLume.Core.Imaging;

/// <summary>Read-only summary of an image file, used by the CLI `info` command.</summary>
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
}
