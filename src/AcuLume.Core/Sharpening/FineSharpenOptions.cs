namespace AcuLume.Core.Sharpening;

/// <summary>
/// Fine-frequency output sharpening settings (spec section 15.1 / 17). Amount of 0 disables the stage.
/// </summary>
public sealed record FineSharpenOptions
{
    /// <summary>Gaussian sigma (px) used for the high-pass extraction.</summary>
    public double Radius { get; init; } = 0.6;

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
            throw new ArgumentOutOfRangeException(nameof(Radius), "Fine radius must be positive.");
        }

        if (Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Amount), "Fine amount must not be negative.");
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
