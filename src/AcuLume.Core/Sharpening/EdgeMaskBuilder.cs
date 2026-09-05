using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Builds a progressive edge-protection mask from a Scharr gradient magnitude (spec section 19).
/// Mask values are in [0, 1]: 0 = flat area, full sharpening allowed; 1 = strong edge, sharpening
/// fully suppressed. The smooth ramp (via <see cref="SoftThreshold"/>) is what keeps this from
/// being an all-or-nothing cutoff.
/// </summary>
public static class EdgeMaskBuilder
{
    // Scharr operator: more rotationally symmetric than Sobel, preferred per spec section 19.
    private static readonly double[,] ScharrX =
    {
        { -3, 0, 3 },
        { -10, 0, 10 },
        { -3, 0, 3 },
    };

    private static readonly double[,] ScharrY =
    {
        { -3, -10, -3 },
        { 0, 0, 0 },
        { 3, 10, 3 },
    };

    public static Image BuildProtectionMask(Image luminance, EdgeProtectionOptions options)
    {
        using var denoised = luminance.Gaussblur(options.DetectionBlur);
        using var gx = Convolve(denoised, ScharrX);
        using var gy = Convolve(denoised, ScharrY);
        using var magnitude = ((gx * gx) + (gy * gy)).Pow(0.5);

        var low = Math.Max(0.0, options.Threshold - (options.Softness / 2.0));
        var high = options.Threshold + (options.Softness / 2.0);
        return SoftThreshold.Smoothstep(magnitude, low, high);
    }

    private static Image Convolve(Image image, double[,] kernel)
    {
        using var mask = Image.NewFromArray(kernel);
        return image.Conv(mask, precision: Enums.Precision.Float);
    }
}
