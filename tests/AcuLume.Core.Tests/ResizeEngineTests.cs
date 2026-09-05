using AcuLume.Core.Imaging;

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
}
