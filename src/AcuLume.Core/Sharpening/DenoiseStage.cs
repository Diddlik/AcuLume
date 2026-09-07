using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Noise reduction on the L channel, placed before output sharpening: the noise that matters is
/// exactly the noise the sharpener is about to amplify.
///
/// This is a scope extension. The specification asks only that sharpening not amplify noise
/// (section 18), and the pipeline satisfies that; removing noise that is already present is a
/// different job, and measurement against Photoshop is what motivated adding it.
///
/// Every engine is composed from libvips operations, so the whole thread pool is used and no
/// managed per-pixel loop exists. Non-local means loops over a few dozen *shifts*, each of which is
/// a whole-image operation — that is what makes it viable here at all.
/// </summary>
public static class DenoiseStage
{
    public static Image Apply(Image rgb, DenoiseOptions options)
    {
        if (!options.IsEnabled)
        {
            return rgb;
        }

        options.Validate();

        var originalInterpretation = rgb.Interpretation;

        using var lab = rgb.Colourspace(Enums.Interpretation.Lab);
        using var band = lab[0];
        using var a = lab[1];
        using var b = lab[2];

        // Measuring the noise and then filtering by it are two passes over the same pixels, which a
        // sequentially-read source will not allow ("out of order read"). The channel is materialised
        // once here rather than loading the whole pipeline in random-access mode; at full resolution
        // that is one float channel, so ~96 MB for a 24 MP frame.
        using var luminance = band.CopyMemory();

        var sigma = options.Sigma ?? NoiseEstimator.EstimateSigma(luminance);
        if (sigma <= 0)
        {
            return rgb;
        }

        using var cleaned = options.Engine switch
        {
            DenoiseEngine.MultiScaleShrinkage => MultiScaleShrinkage(luminance, options, sigma),
            DenoiseEngine.NonLocalMeans => NonLocalMeans(luminance, options, sigma),
            _ => GuidedFilter(luminance, options, sigma),
        };

        // Blend rather than replace, so the amount is a continuous control and the engines stay
        // comparable at partial strength.
        using var blended = Blend(luminance, cleaned, options.Amount);
        using var resultLab = blended.Bandjoin(a, b);
        return resultLab.Colourspace(originalInterpretation);
    }

    private static Image Blend(Image original, Image cleaned, double amount)
    {
        if (amount >= 1.0)
        {
            return cleaned.Copy();
        }

        using var keep = original * (1.0 - amount);
        using var take = cleaned * amount;
        return keep + take;
    }

    /// <summary>
    /// Separable box mean. Convsep applies a one-dimensional mask twice, so an n-tap box costs two
    /// passes rather than n², and libvips runs it tiled across the thread pool.
    /// </summary>
    private static Image BoxMean(Image image, int radius)
    {
        var width = (2 * radius) + 1;
        var row = new double[width];
        Array.Fill(row, 1.0);

        using var mask = Image.NewFromArray(row, scale: width);
        return image.Convsep(mask, precision: Enums.Precision.Float);
    }

    /// <summary>
    /// Guided filter with the image as its own guide. Every term is a box mean, so the cost does
    /// not grow with the radius:
    /// <code>
    /// a = var / (var + eps),  b = (1 - a) * mean,  q = mean(a) * I + mean(b)
    /// </code>
    /// <c>eps</c> is the variance below which a neighbourhood counts as flat, so setting it to
    /// (threshold * sigma)² is what ties the strength to the measured noise level.
    /// </summary>
    private static Image GuidedFilter(Image luminance, DenoiseOptions options, double sigma)
    {
        var radius = (int)Math.Round(options.Radius);
        var eps = Math.Pow(options.Threshold * sigma, 2);

        using var mean = BoxMean(luminance, radius);
        using var square = luminance * luminance;
        using var meanSquare = BoxMean(square, radius);
        using var meanTimesMean = mean * mean;
        using var variance = meanSquare - meanTimesMean;

        using var denominator = variance + eps;
        using var aCoefficient = variance / denominator;
        using var oneMinusA = 1.0 - aCoefficient;
        using var bCoefficient = oneMinusA * mean;

        using var meanA = BoxMean(aCoefficient, radius);
        using var meanB = BoxMean(bCoefficient, radius);
        using var scaled = meanA * luminance;
        return scaled + meanB;
    }

    /// <summary>
    /// Soft thresholding across successive detail scales (an à trous decomposition). Each level
    /// holds what one blur removed; shrinking that by the noise threshold and summing the levels
    /// back reconstructs the image without the part that looked like grain at any scale.
    ///
    /// Noise power falls as the scales coarsen, so the threshold halves per level; without that the
    /// coarse levels would be over-shrunk and leave low-frequency mottling.
    /// </summary>
    private static Image MultiScaleShrinkage(Image luminance, DenoiseOptions options, double sigma)
    {
        const int levels = 3;

        var residual = luminance.Copy();
        Image? accumulated = null;
        var scale = options.Radius / 2.0;
        var threshold = options.Threshold * sigma;

        try
        {
            for (var level = 0; level < levels; level++)
            {
                using var blurred = residual.Gaussblur(scale, minAmpl: 0.005, precision: Enums.Precision.Float);
                using var detail = residual - blurred;
                using var shrunk = SoftShrink(detail, threshold);

                var next = accumulated is null ? shrunk.Copy() : accumulated + shrunk;
                accumulated?.Dispose();
                accumulated = next;

                residual.Dispose();
                residual = blurred.Copy();

                scale *= 2.0;
                threshold /= 2.0;
            }

            return accumulated! + residual;
        }
        finally
        {
            accumulated?.Dispose();
            residual.Dispose();
        }
    }

    /// <summary>
    /// Classic soft shrinkage: pull every value toward zero by <paramref name="threshold"/> and
    /// clamp there. Unlike a multiplicative weight it removes the noise floor outright instead of
    /// only attenuating it, which is the difference between protection and reduction.
    /// </summary>
    private static Image SoftShrink(Image detail, double threshold)
    {
        using var magnitude = detail.Abs();
        using var reduced = magnitude - threshold;
        using var clamped = (reduced < 0).Ifthenelse(0.0, reduced);
        using var sign = detail.Sign();
        return sign * clamped;
    }

    /// <summary>
    /// Non-local means, accumulated over shifts. For each offset in the search window the whole
    /// image is compared with itself displaced by that offset; the box mean of the squared
    /// difference is the patch distance, and its exponential is the weight. The loop runs over a few
    /// dozen offsets, each a whole-image operation, so nothing here is a per-pixel loop.
    ///
    /// The published advantage of this method is measured on additive white Gaussian noise. Our
    /// input is demosaiced, JPEG-compressed and — after the downscale — spatially correlated, which
    /// is exactly the assumption patch weighting relies on. Whether the advantage survives that is
    /// what the comparison has to answer.
    /// </summary>
    private static Image NonLocalMeans(Image luminance, DenoiseOptions options, double sigma)
    {
        var search = (int)Math.Round(options.Radius);
        var patch = Math.Max(1, search - 1);
        var h2 = Math.Pow(options.Threshold * sigma, 2);
        var noiseFloor = 2.0 * sigma * sigma;

        var width = luminance.Width;
        var height = luminance.Height;
        using var padded = luminance.Embed(
            search, search, width + (2 * search), height + (2 * search), extend: Enums.Extend.Copy);

        Image? numerator = null;
        Image? denominator = null;

        try
        {
            for (var dy = -search; dy <= search; dy++)
            {
                for (var dx = -search; dx <= search; dx++)
                {
                    using var shifted = padded.Crop(search + dx, search + dy, width, height);
                    using var difference = luminance - shifted;
                    using var squared = difference * difference;
                    using var distance = BoxMean(squared, patch);

                    // Subtracting the noise floor keeps two identically noisy patches from being
                    // scored as different merely because both carry noise.
                    using var excess = distance - noiseFloor;
                    using var positive = (excess < 0).Ifthenelse(0.0, excess);
                    using var exponent = positive / -h2;
                    using var weight = exponent.Math(Enums.OperationMath.Exp);
                    using var weighted = weight * shifted;

                    var nextNumerator = numerator is null ? weighted.Copy() : numerator + weighted;
                    numerator?.Dispose();
                    numerator = nextNumerator;

                    var nextDenominator = denominator is null ? weight.Copy() : denominator + weight;
                    denominator?.Dispose();
                    denominator = nextDenominator;
                }
            }

            return numerator! / denominator!;
        }
        finally
        {
            numerator?.Dispose();
            denominator?.Dispose();
        }
    }
}
