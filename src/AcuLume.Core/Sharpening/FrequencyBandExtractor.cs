using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>Gaussian high-pass decomposition (spec section 16).</summary>
public static class FrequencyBandExtractor
{
    /// <summary>
    /// libvips truncates the Gaussian where it falls below <c>min-ampl</c>, and at the default 0.2
    /// every sigma under ~0.6 collapses to a 1×1 mask — an identity blur, so the high-pass would be
    /// exactly zero and the whole band silently dead. Sub-pixel radii are the point of the fine
    /// band, so the kernel is cut much further out instead. Integer precision then rounds the small
    /// off-centre weights back to zero, so the convolution runs in float as well.
    /// </summary>
    private const double MinAmplitude = 0.005;

    /// <summary>Returns <paramref name="channel"/> minus its Gaussian blur at the given sigma.</summary>
    public static Image ExtractHighPass(Image channel, double sigma)
    {
        using var blurred = channel.Gaussblur(sigma, minAmpl: MinAmplitude, precision: Enums.Precision.Float);
        return channel - blurred;
    }
}
