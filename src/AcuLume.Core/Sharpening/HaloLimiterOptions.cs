namespace AcuLume.Core.Sharpening;

/// <summary>
/// Halo limiter settings (spec section 20): caps sharpening overshoot/undershoot to a fraction of
/// the local contrast range, instead of relying on final-image clipping. Separate dark/light
/// limits because bright halos are usually more objectionable (spec: "light limit" default is
/// tighter than "dark limit"). Amount of 0 disables it.
/// </summary>
public sealed record HaloLimiterOptions
{
    /// <summary>Overall strength (0-1), blending between the raw contribution and the fully clamped one.</summary>
    public double Amount { get; init; }

    /// <summary>Half-width (px) of the local min/max window used to estimate local contrast range.</summary>
    public double WindowRadius { get; init; } = 2.0;

    /// <summary>Max allowed undershoot as a fraction of the local (max - min) range.</summary>
    public double DarkLimit { get; init; } = 0.5;

    /// <summary>Max allowed overshoot as a fraction of the local (max - min) range. Tighter than dark by default.</summary>
    public double LightLimit { get; init; } = 0.3;

    public bool IsEnabled => Amount > 0;

    public void Validate()
    {
        if (Amount is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "Halo limiter amount must be between 0 and 1.");
        }

        if (WindowRadius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(WindowRadius), "Halo limiter window radius must be positive.");
        }

        if (DarkLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DarkLimit), "Dark halo limit must not be negative.");
        }

        if (LightLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(LightLimit), "Light halo limit must not be negative.");
        }
    }
}
