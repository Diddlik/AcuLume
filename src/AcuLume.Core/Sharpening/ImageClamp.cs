using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>Per-pixel clamp helpers shared by <see cref="SoftThreshold"/> and <see cref="HaloLimiter"/>.</summary>
public static class ImageClamp
{
    public static Image Clamp(Image value, double lower, double upper)
    {
        using var belowLower = value < lower;
        using var clampedLow = belowLower.Ifthenelse(lower, value);
        using var aboveUpper = clampedLow > upper;
        return aboveUpper.Ifthenelse(upper, clampedLow);
    }

    /// <summary>Per-pixel bounds given as images (e.g. a locally varying halo limit).</summary>
    public static Image Clamp(Image value, Image lower, Image upper)
    {
        using var belowLower = value < lower;
        using var clampedLow = belowLower.Ifthenelse(lower, value);
        using var aboveUpper = clampedLow > upper;
        return aboveUpper.Ifthenelse(upper, clampedLow);
    }
}
