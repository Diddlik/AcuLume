namespace AcuLume.Core.Sharpening;

/// <summary>
/// Soft noise protection (spec section 18): suppresses small-magnitude high-pass detail
/// (likely noise) via a smooth threshold rather than a harsh binary cutoff, so real texture near
/// the threshold is not visibly discontinuous. Applied to every band's raw detail before the
/// dark/light split. Amount of 0 disables it (full detail always passes through).
/// </summary>
public sealed record NoiseProtectionOptions
{
    /// <summary>Overall protection strength (0-1). 0 disables noise protection entirely.</summary>
    public double Amount { get; init; }

    /// <summary>Detail magnitude (Lab L units) above which detail is treated as real, not noise.</summary>
    public double Threshold { get; init; } = 1.0;

    /// <summary>Width of the smoothstep ramp (Lab L units) around the threshold.</summary>
    public double Softness { get; init; } = 1.5;

    public bool IsEnabled => Amount > 0;

    public void Validate()
    {
        if (Amount is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "Noise protection amount must be between 0 and 1.");
        }

        if (Threshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Threshold), "Noise threshold must not be negative.");
        }

        if (Softness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Softness), "Noise softness must be positive.");
        }
    }
}
