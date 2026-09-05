using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Applies the soft noise threshold to raw (pre-split) band detail (spec section 18):
/// <c>weight = smoothstep(noiseLow, noiseHigh, abs(detail))</c>, then <c>detail *= weight</c>.
/// </summary>
public static class NoiseProtection
{
    public static Image Apply(Image detail, NoiseProtectionOptions options)
    {
        if (!options.IsEnabled)
        {
            return detail.Copy();
        }

        var low = Math.Max(0.0, options.Threshold - (options.Softness / 2.0));
        var high = options.Threshold + (options.Softness / 2.0);

        using var magnitude = detail.Abs();
        using var weight = SoftThreshold.Smoothstep(magnitude, low, high);

        // amount blends between "always pass" (weight = 1) and the full soft-threshold curve.
        using var effectiveWeight = 1.0 - (options.Amount * (1.0 - weight));
        return detail * effectiveWeight;
    }
}
