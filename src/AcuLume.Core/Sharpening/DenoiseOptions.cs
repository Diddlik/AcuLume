namespace AcuLume.Core.Sharpening;

/// <summary>
/// Which noise reduction engine runs. All three are composed from libvips operations, so each one
/// runs on the whole thread pool with no managed per-pixel loop.
/// </summary>
public enum DenoiseEngine
{
    /// <summary>
    /// Guided filter (He 2010): a local linear model of the image against itself, built from box
    /// means. Edge-preserving, one pass, cost independent of the window size.
    /// </summary>
    GuidedFilter,

    /// <summary>
    /// Soft thresholding of successive detail scales — the same shrinkage idea as the existing
    /// noise protection, applied across several octaves instead of one band.
    /// </summary>
    MultiScaleShrinkage,

    /// <summary>
    /// Non-local means, accumulated over a fixed set of shifts rather than a per-pixel patch
    /// search. The strongest of the three on paper, and the most expensive by an order of magnitude.
    /// </summary>
    NonLocalMeans,
}

/// <summary>
/// Noise reduction settings. Every threshold is a multiple of the noise sigma measured from the
/// image by <see cref="NoiseEstimator"/>, not an absolute constant, so one setting means the same
/// thing on a base-ISO landscape and a high-ISO portrait.
/// </summary>
public sealed record DenoiseOptions
{
    public DenoiseEngine Engine { get; init; } = DenoiseEngine.GuidedFilter;

    /// <summary>Overall strength (0-1). 0 disables the stage; 1 applies the engine's full result.</summary>
    public double Amount { get; init; }

    /// <summary>
    /// How many sigmas of detail count as noise. Higher removes more and costs texture; this is the
    /// single knob that trades the noise floor against acutance.
    /// </summary>
    public double Threshold { get; init; } = 1.5;

    /// <summary>Radius of the neighbourhood each engine reasons over, in pixels.</summary>
    public double Radius { get; init; } = 2.0;

    /// <summary>
    /// Overrides the measured sigma. Left null the stage estimates it per image, which is the point
    /// of the design; a fixed value exists so a measurement run can hold it constant.
    /// </summary>
    public double? Sigma { get; init; }

    public bool IsEnabled => Amount > 0;

    public void Validate()
    {
        if (Amount is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "Denoise amount must be between 0 and 1.");
        }

        if (Threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Threshold), "Denoise threshold must be positive.");
        }

        if (Radius is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(Radius), "Denoise radius must be between 1 and 8 px.");
        }

        if (Sigma is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Sigma), "Sigma override must be positive when given.");
        }
    }
}
