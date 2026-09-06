namespace AcuLume.Core.Sharpening;

/// <summary>How much capture sharpening to apply (spec section 21). Off is the default.</summary>
public enum CaptureSharpenLevel
{
    Off,
    Low,
    Normal,
}

/// <summary>
/// Which restoration engine runs (spec section 21 / Phase 4). The spec is explicit that
/// deconvolution stays a separate engine and must not be folded into the stable path.
/// </summary>
public enum CaptureSharpenEngine
{
    /// <summary>Conservative fine-frequency restoration — the spec's starting point.</summary>
    FineRestore,

    /// <summary>Richardson-Lucy deconvolution against an assumed Gaussian PSF. Experimental.</summary>
    RichardsonLucy,
}

/// <summary>
/// Capture sharpening settings (spec section 21): light restoration at capture resolution, before
/// the image is resized for output. Deliberately subtle — it corrects lens and sensor softness, it
/// does not do the job of output sharpening.
/// </summary>
public sealed record CaptureSharpenOptions
{
    public CaptureSharpenLevel Level { get; init; } = CaptureSharpenLevel.Off;

    public CaptureSharpenEngine Engine { get; init; } = CaptureSharpenEngine.FineRestore;

    /// <summary>Assumed blur radius in pixels: the high-pass sigma, or the PSF sigma for deconvolution.</summary>
    public double Radius { get; init; } = 0.8;

    /// <summary>Richardson-Lucy iteration count. More iterations restore more and amplify noise faster.</summary>
    public int Iterations { get; init; } = 4;

    public bool IsEnabled => Level != CaptureSharpenLevel.Off;

    /// <summary>
    /// Strength for the configured level. Capture sharpening has to survive a downscale that throws
    /// most of it away, without being visible as sharpening in its own right at 100%.
    /// </summary>
    public double Amount => Level switch
    {
        CaptureSharpenLevel.Low => 0.30,
        CaptureSharpenLevel.Normal => 0.60,
        _ => 0.0,
    };

    public void Validate()
    {
        if (Radius < BandSharpenOptions.MinimumRadius)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Radius),
                $"Capture sharpen radius must be at least {BandSharpenOptions.MinimumRadius} px.");
        }

        if (Iterations is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Iterations), "Richardson-Lucy iterations must be between 1 and 20.");
        }
    }
}
