using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>Gaussian high-pass decomposition (spec section 16).</summary>
public static class FrequencyBandExtractor
{
    /// <summary>Returns <paramref name="channel"/> minus its Gaussian blur at the given sigma.</summary>
    public static Image ExtractHighPass(Image channel, double sigma)
    {
        using var blurred = channel.Gaussblur(sigma);
        return channel - blurred;
    }
}
