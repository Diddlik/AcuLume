namespace AcuLume.Core.Sharpening;

/// <summary>
/// Output sharpening settings, applied after the final resize (spec section 23). Fine + medium
/// bands today; a coarse band can be added later without redesigning the pipeline (spec 15.3).
/// </summary>
public sealed record OutputSharpenOptions
{
    public BandSharpenOptions Fine { get; init; } = new() { Radius = 0.6, Amount = 0 };

    /// <summary>Medium band is generally weaker than fine (spec section 15.2) and disabled by default.</summary>
    public BandSharpenOptions Medium { get; init; } = new() { Radius = 1.4, Amount = 0 };

    /// <summary>Protects strong edges from halos (spec section 19). Disabled by default.</summary>
    public EdgeProtectionOptions EdgeProtection { get; init; } = new();

    /// <summary>Suppresses small-magnitude detail likely to be noise (spec section 18). Disabled by default.</summary>
    public NoiseProtectionOptions NoiseProtection { get; init; } = new();

    public bool IsEnabled => Fine.IsEnabled || Medium.IsEnabled;
}
