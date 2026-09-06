using AcuLume.Core.Imaging;
using NetVips;

namespace AcuLume.Core.Tests;

public class ResizeEngineTests
{
    [Theory]
    [InlineData(6000, 4000, 1800, 1800, 1200)]
    [InlineData(4000, 6000, 1800, 1200, 1800)]
    [InlineData(1000, 1000, 1800, 1000, 1000)] // no upscale by default
    public void CalculateLongEdgeDimensions_PreservesAspectAndRespectsUpscalePolicy(
        int width, int height, int longEdge, int expectedWidth, int expectedHeight)
    {
        var (actualWidth, actualHeight) = ResizeEngine.CalculateLongEdgeDimensions(
            width, height, longEdge, allowUpscale: false);

        Assert.Equal(expectedWidth, actualWidth);
        Assert.Equal(expectedHeight, actualHeight);
    }

    [Fact]
    public void CalculateLongEdgeDimensions_AllowsUpscaleWhenRequested()
    {
        var (width, height) = ResizeEngine.CalculateLongEdgeDimensions(
            1000, 500, 2000, allowUpscale: true);

        Assert.Equal(2000, width);
        Assert.Equal(1000, height);
    }

    [Theory]
    [InlineData(0, 100, 1800)]
    [InlineData(100, 0, 1800)]
    [InlineData(100, 100, 0)]
    public void CalculateLongEdgeDimensions_RejectsNonPositiveInputs(int width, int height, int longEdge)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ResizeEngine.CalculateLongEdgeDimensions(width, height, longEdge, allowUpscale: false));
    }

    /// <summary>
    /// Regression: chaining resize steps accumulates rounding, and a staged downscale once landed on
    /// 1801 px where a single step gave 1800. Every strategy/space combination has to produce the
    /// dimensions CalculateLongEdgeDimensions promises.
    /// </summary>
    [Theory]
    [InlineData(ResizeStrategy.Single, ResizeSpace.Gamma)]
    [InlineData(ResizeStrategy.Staged, ResizeSpace.Gamma)]
    [InlineData(ResizeStrategy.Single, ResizeSpace.LinearLight)]
    [InlineData(ResizeStrategy.Staged, ResizeSpace.LinearLight)]
    public void ResizeToLongEdge_HitsTheCalculatedDimensions(ResizeStrategy strategy, ResizeSpace space)
    {
        // An awkward aspect ratio: the residual factor after halving does not round cleanly.
        using var source = (Image.Black(5179, 3453, bands: 3) + 128).Copy(interpretation: Enums.Interpretation.Srgb);
        var (expectedWidth, expectedHeight) =
            ResizeEngine.CalculateLongEdgeDimensions(source.Width, source.Height, 1800, allowUpscale: false);

        using var resized = ResizeEngine.ResizeToLongEdge(source, 1800, allowUpscale: false, strategy, space);

        Assert.Equal(expectedWidth, resized.Width);
        Assert.Equal(expectedHeight, resized.Height);
        Assert.Equal(1800, Math.Max(resized.Width, resized.Height));
    }
}
