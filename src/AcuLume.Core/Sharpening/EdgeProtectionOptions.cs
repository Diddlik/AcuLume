namespace AcuLume.Core.Sharpening;

/// <summary>
/// Edge protection settings (spec section 19). Strong edges (dark building against bright sky,
/// text against a plain background, ...) are common sources of ugly sharpening halos; this mask
/// progressively attenuates — never fully suppresses — sharpening near them. Amount of 0 disables it.
/// </summary>
public sealed record EdgeProtectionOptions
{
    /// <summary>Overall protection strength (0-1). 0 disables edge protection entirely.</summary>
    public double Amount { get; init; }

    /// <summary>Gradient magnitude (Lab L units) above which protection ramps up.</summary>
    public double Threshold { get; init; } = 15.0;

    /// <summary>Width of the ramp (Lab L units) around the threshold — the smoothstep transition band.</summary>
    public double Softness { get; init; } = 10.0;

    /// <summary>Gaussian sigma (px) used to denoise luminance before computing the gradient.</summary>
    public double DetectionBlur { get; init; } = 1.0;

    public bool IsEnabled => Amount > 0;

    public void Validate()
    {
        if (Amount is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "Edge protection amount must be between 0 and 1.");
        }

        if (Threshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Threshold), "Edge threshold must not be negative.");
        }

        if (Softness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Softness), "Edge softness must be positive.");
        }

        if (DetectionBlur <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DetectionBlur), "Edge detection blur must be positive.");
        }
    }
}
