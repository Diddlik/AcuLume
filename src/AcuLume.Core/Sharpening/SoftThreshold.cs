using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Smooth (non-binary) thresholding, shared by edge and noise protection
/// (spec sections 18-19: "prefer a smooth transition such as smoothstep ... over a harsh binary cutoff").
/// </summary>
public static class SoftThreshold
{
    /// <summary>Classic smoothstep: 0 below <paramref name="low"/>, 1 above <paramref name="high"/>, eased between.</summary>
    public static Image Smoothstep(Image value, double low, double high)
    {
        using var x = ImageClamp.Clamp((value - low) / (high - low), 0.0, 1.0);
        using var threeMinusTwoX = 3.0 - (2.0 * x);
        return x * x * threeMinusTwoX;
    }
}
