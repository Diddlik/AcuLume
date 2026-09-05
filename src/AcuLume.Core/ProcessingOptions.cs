namespace AcuLume.Core;

/// <summary>
/// Options for a single-image processing run. Phase 0 only supports the resize baseline —
/// no sharpening fields yet (see docs/AcuLume_IMPLEMENTATION_REQUIREMENTS.md Task 3/4).
/// </summary>
public sealed record ProcessingOptions
{
    /// <summary>Target long-edge size in pixels. Null means no resize.</summary>
    public int? LongEdge { get; init; }

    public bool AllowUpscale { get; init; }

    /// <summary>JPEG quality (1-100). Ignored for lossless formats.</summary>
    public int Quality { get; init; } = 90;
}
