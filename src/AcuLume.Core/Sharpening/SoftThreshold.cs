using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Smooth (non-binary) thresholding, shared by edge protection and, later, noise protection
/// (spec sections 18-19: "prefer a smooth transition such as smoothstep ... over a harsh binary cutoff").
/// </summary>
public static class SoftThreshold
{
    /// <summary>Classic smoothstep: 0 below <paramref name="low"/>, 1 above <paramref name="high"/>, eased between.</summary>
    public static Image Smoothstep(Image value, double low, double high)
    {
        using var x = Clamp01((value - low) / (high - low));
        using var threeMinusTwoX = 3.0 - (2.0 * x);
        return x * x * threeMinusTwoX;
    }

    private static Image Clamp01(Image x)
    {
        using var belowZero = x < 0.0;
        using var clampedLow = belowZero.Ifthenelse(0.0, x);
        using var aboveOne = clampedLow > 1.0;
        return aboveOne.Ifthenelse(1.0, clampedLow);
    }
}
