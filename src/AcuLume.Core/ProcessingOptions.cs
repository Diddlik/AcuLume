using AcuLume.Core.Sharpening;

namespace AcuLume.Core;

/// <summary>Options for a single-image processing run.</summary>
public sealed record ProcessingOptions
{
    /// <summary>Target long-edge size in pixels. Null means no resize.</summary>
    public int? LongEdge { get; init; }

    public bool AllowUpscale { get; init; }

    /// <summary>JPEG quality (1-100). Ignored for lossless formats.</summary>
    public int Quality { get; init; } = 90;

    /// <summary>Output sharpening (fine + medium bands), applied after resize. Disabled by default.</summary>
    public OutputSharpenOptions OutputSharpen { get; init; } = new();
}
