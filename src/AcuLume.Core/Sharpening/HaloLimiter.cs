using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Caps sharpening overshoot/undershoot to a fraction of the local contrast range (spec section 20)
/// instead of relying on final-image clipping. Local min/max come from an order-statistic ("rank")
/// filter over a small window on the pre-sharpening luminance.
/// </summary>
public static class HaloLimiter
{
    public static Image Apply(Image luminance, Image contribution, HaloLimiterOptions options)
    {
        if (!options.IsEnabled)
        {
            return contribution.Copy();
        }

        var windowSize = Math.Max(3, (2 * (int)Math.Round(options.WindowRadius)) + 1);

        using var localMax = luminance.Rank(windowSize, windowSize, (windowSize * windowSize) - 1);
        using var localMin = luminance.Rank(windowSize, windowSize, 0);
        using var localRange = localMax - localMin;

        using var maxUndershoot = localRange * options.DarkLimit;
        using var maxOvershoot = localRange * options.LightLimit;
        using var minAllowed = maxUndershoot * -1.0;

        using var clamped = ImageClamp.Clamp(contribution, minAllowed, maxOvershoot);

        using var originalPart = contribution * (1.0 - options.Amount);
        using var clampedPart = clamped * options.Amount;
        return originalPart + clampedPart;
    }
}
