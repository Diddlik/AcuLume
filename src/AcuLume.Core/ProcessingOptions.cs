using AcuLume.Core.Imaging;
using AcuLume.Core.Sharpening;

namespace AcuLume.Core;

/// <summary>Options for a single-image processing run.</summary>
public sealed record ProcessingOptions
{
    /// <summary>Target long-edge size in pixels. Null means no resize.</summary>
    public int? LongEdge { get; init; }

    public bool AllowUpscale { get; init; }

    /// <summary>Downscale strategy. Experimental (spec Phase 3); the default is unchanged.</summary>
    public ResizeStrategy ResizeStrategy { get; init; } = ResizeStrategy.Single;

    /// <summary>Encoding the resampling happens in. Experimental (spec Phase 3); the default is unchanged.</summary>
    public ResizeSpace ResizeSpace { get; init; } = ResizeSpace.Gamma;

    /// <summary>JPEG quality (1-100). Ignored for lossless formats.</summary>
    public int Quality { get; init; } = 90;

    /// <summary>Capture sharpening, applied at capture resolution before the resize. Off by default.</summary>
    public CaptureSharpenOptions CaptureSharpen { get; init; } = new();

    /// <summary>Output sharpening (fine + medium bands), applied after resize. Disabled by default.</summary>
    public OutputSharpenOptions OutputSharpen { get; init; } = new();
}
