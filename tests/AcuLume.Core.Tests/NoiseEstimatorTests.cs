using AcuLume.Core.Sharpening;
using NetVips;

namespace AcuLume.Core.Tests;

public class NoiseEstimatorTests
{
    [Fact]
    public void EstimateSigma_IsNearZero_ForAFlatField()
    {
        using var flat = (Image.Black(256, 256) + 50).Cast(Enums.BandFormat.Float);

        Assert.True(NoiseEstimator.EstimateSigma(flat) < 0.05);
    }

    /// <summary>The estimate has to track the noise level, not merely be non-zero for noisy input.</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(3.0)]
    [InlineData(6.0)]
    public void EstimateSigma_TracksTheNoiseItWasGiven(double sigma)
    {
        using var noise = Image.Gaussnoise(400, 400, sigma: sigma, mean: 50, seed: 7);

        var estimate = NoiseEstimator.EstimateSigma(noise);

        // The high-pass keeps only part of the noise power, so the estimate is a consistent fraction
        // of the true sigma rather than equal to it; what matters is that it scales with it.
        Assert.InRange(estimate / sigma, 0.3, 1.2);
    }

    [Fact]
    public void EstimateSigma_RisesWithNoise()
    {
        using var quiet = Image.Gaussnoise(400, 400, sigma: 1.0, mean: 50, seed: 7);
        using var loud = Image.Gaussnoise(400, 400, sigma: 5.0, mean: 50, seed: 7);

        Assert.True(NoiseEstimator.EstimateSigma(loud) > NoiseEstimator.EstimateSigma(quiet) * 3);
    }

    /// <summary>
    /// The point of a median-based estimator: strong structure sits in the tail of the distribution,
    /// so an edge must not be mistaken for noise.
    /// </summary>
    [Fact]
    public void EstimateSigma_IsNotFooledByAStrongEdge()
    {
        using var dark = (Image.Black(200, 400) + 20).Cast(Enums.BandFormat.Float);
        using var light = (Image.Black(200, 400) + 90).Cast(Enums.BandFormat.Float);
        using var edge = dark.Join(light, Enums.Direction.Horizontal);

        Assert.True(NoiseEstimator.EstimateSigma(edge) < 0.5);
    }
}
