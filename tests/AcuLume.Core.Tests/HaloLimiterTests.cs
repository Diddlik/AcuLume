using AcuLume.Core.Sharpening;
using NetVips;

namespace AcuLume.Core.Tests;

public class HaloLimiterTests
{
    [Fact]
    public void Apply_ClampsContributionToFractionOfLocalRange()
    {
        const int halfWidth = 40;
        const int height = 20;
        const double darkValue = 50;
        const double lightValue = 200; // local range near the edge ~150

        using var luminance = MakeStepEdge(halfWidth, height, darkValue, lightValue);
        using var hugeOvershoot = (Image.Black(halfWidth * 2, height) + 200.0).Cast(Enums.BandFormat.Float);

        var options = new HaloLimiterOptions { Amount = 1.0, WindowRadius = 3.0, DarkLimit = 0.5, LightLimit = 0.3 };

        using var clamped = HaloLimiter.Apply(luminance, hugeOvershoot, options);

        var nearEdge = ReadPixel(clamped, halfWidth - 1, height / 2);
        var flatInterior = ReadPixel(clamped, 5, height / 2);

        // Local range near the edge is large, so some contribution survives, but far less than the raw 200.
        Assert.True(nearEdge is > 0 and < 100, $"Expected a reduced but nonzero contribution near the edge, got {nearEdge}");
        // Local range in a flat area is ~0, so the allowed overshoot is ~0 too.
        Assert.True(flatInterior < 5, $"Expected near-zero contribution in a flat area, got {flatInterior}");
    }

    [Fact]
    public void Apply_AllowsMoreUndershootThanOvershoot_WhenLightLimitIsTighter()
    {
        const int halfWidth = 40;
        const int height = 20;

        using var luminance = MakeStepEdge(halfWidth, height, darkValue: 50, lightValue: 200);
        using var positiveContribution = (Image.Black(halfWidth * 2, height) + 200.0).Cast(Enums.BandFormat.Float);
        using var negativeContribution = positiveContribution * -1.0;

        var options = new HaloLimiterOptions { Amount = 1.0, WindowRadius = 3.0, DarkLimit = 0.5, LightLimit = 0.3 };

        using var clampedPositive = HaloLimiter.Apply(luminance, positiveContribution, options);
        using var clampedNegative = HaloLimiter.Apply(luminance, negativeContribution, options);

        var overshoot = ReadPixel(clampedPositive, halfWidth - 1, height / 2);
        var undershoot = Math.Abs(ReadPixel(clampedNegative, halfWidth - 1, height / 2));

        Assert.True(undershoot > overshoot,
            $"Expected the allowed undershoot ({undershoot}) to exceed the allowed overshoot ({overshoot}) since DarkLimit > LightLimit");
    }

    [Fact]
    public void Apply_IsNoOp_WhenAmountIsZero()
    {
        using var luminance = MakeStepEdge(20, 10, 50, 200);
        using var contribution = (Image.Black(40, 10) + 200.0).Cast(Enums.BandFormat.Float);
        var options = new HaloLimiterOptions { Amount = 0 };

        using var result = HaloLimiter.Apply(luminance, contribution, options);

        Assert.Equal(200.0, ReadPixel(result, 5, 5), 3);
    }

    private static Image MakeStepEdge(int halfWidth, int height, double darkValue, double lightValue)
    {
        using var dark = (Image.Black(halfWidth, height) + darkValue).Cast(Enums.BandFormat.Float);
        using var light = (Image.Black(halfWidth, height) + lightValue).Cast(Enums.BandFormat.Float);
        return dark.Join(light, Enums.Direction.Horizontal);
    }

    private static double ReadPixel(Image image, int x, int y) => image.Getpoint(x, y)[0];
}
