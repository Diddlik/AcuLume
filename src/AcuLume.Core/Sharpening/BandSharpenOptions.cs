namespace AcuLume.Core.Sharpening;

/// <summary>
/// Settings for a single frequency band (fine or medium — spec section 15/17). Amount of 0 disables the band.
/// </summary>
public sealed record BandSharpenOptions
{
    /// <summary>Gaussian sigma (px) used for the high-pass extraction.</summary>
    public required double Radius { get; init; }

    /// <summary>Overall strength multiplier applied after the dark/light split.</summary>
    public double Amount { get; init; }

    /// <summary>Weight applied to negative (darkening) detail. Spec default: stronger than light.</summary>
    public double DarkAmount { get; init; } = 0.8;

    /// <summary>Weight applied to positive (lightening) detail.</summary>
    public double LightAmount { get; init; } = 0.5;

    public bool IsEnabled => Amount > 0;

    public void Validate()
    {
        if (Radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Radius), "Radius must be positive.");
        }

        if (Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "Amount must not be negative.");
        }

        if (DarkAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DarkAmount), "Dark amount must not be negative.");
        }

        if (LightAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LightAmount), "Light amount must not be negative.");
        }
    }
}
