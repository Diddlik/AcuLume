using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Estimates the noise level of an image from the image itself, so thresholds can be expressed as
/// multiples of the grain actually present rather than as absolute constants.
///
/// This matters because the shipped <c>noiseThreshold</c> is an absolute value in Lab L units and is
/// therefore the same number for a base-ISO landscape and a high-ISO portrait — which is why moving
/// it changed nothing measurable during calibration.
///
/// The estimator is the standard robust one: the median absolute deviation of the finest detail
/// band, divided by 0.6745 to convert a median deviation into a Gaussian sigma. It is robust because
/// real image structure occupies the tail of the distribution, not its middle, so edges and texture
/// barely move the median.
/// </summary>
public static class NoiseEstimator
{
    /// <summary>Converts a median absolute deviation into the sigma of a normal distribution.</summary>
    private const double MadToSigma = 1.0 / 0.6745;

    /// <summary>
    /// The finest band the pipeline works at. The estimate must come from the scale the sharpener
    /// amplifies, not from a coarser one where real structure dominates.
    /// </summary>
    private const double DetailRadius = 0.5;

    /// <summary>
    /// Detail magnitudes above this are treated as full scale when building the histogram, which
    /// sets both the resolution of the estimate and the point where it saturates. Measured across
    /// the corpus, median magnitudes run 0.08 to 0.6 in Lab L, so 4.0 leaves room for far noisier
    /// frames while keeping a bin worth 0.016 — an earlier ceiling of 10 quantised half the corpus
    /// onto the same two bins. A frame noisy enough to saturate reports a floor, which is the safe
    /// direction for a threshold.
    ///
    /// A fixed ceiling keeps the estimate to a single pass, so it works under sequential access;
    /// measuring the real maximum first would need a second pass the streaming pipeline cannot give.
    /// </summary>
    private const double MagnitudeCeiling = 4.0;

    /// <summary>
    /// Noise sigma of <paramref name="luminance"/>, in the units of that channel — Lab L, so 0-100.
    /// </summary>
    public static double EstimateSigma(Image luminance)
    {
        using var detail = FrequencyBandExtractor.ExtractHighPass(luminance, DetailRadius);
        using var magnitude = detail.Abs();

        return Median(magnitude) * MadToSigma;
    }

    /// <summary>
    /// libvips finds percentiles through a histogram, which needs an integer image, so the
    /// magnitudes are scaled into 0-255 first and the result scaled back. A histogram bin is a
    /// coarse unit, but this estimate feeds a threshold, not a measurement.
    /// </summary>
    private static double Median(Image magnitude)
    {
        const double scale = 255.0 / MagnitudeCeiling;
        using var scaled = (magnitude * scale).Cast(Enums.BandFormat.Uchar);

        return scaled.Percent(50) / scale;
    }
}
