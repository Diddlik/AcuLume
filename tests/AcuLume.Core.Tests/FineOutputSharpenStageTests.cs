using AcuLume.Core.Sharpening;
using NetVips;

namespace AcuLume.Core.Tests;

public class FineOutputSharpenStageTests
{
    /// <summary>
    /// Synthetic black/white edge (spec section 41.2 / Task 4). Verifies the required dark/light
    /// asymmetric split: with darkAmount > lightAmount, the undershoot on the dark side of the
    /// edge must be larger in magnitude than the overshoot on the light side.
    /// </summary>
    [Fact]
    public void Apply_ProducesLargerUndershootThanOvershoot_WhenDarkAmountExceedsLightAmount()
    {
        const int halfWidth = 40;
        const int height = 20;
        const double darkValue = 50;
        const double lightValue = 200;

        using var edge = MakeStepEdge(halfWidth, height, darkValue, lightValue);

        var options = new FineSharpenOptions
        {
            Radius = 2.0,
            Amount = 1.0,
            DarkAmount = 0.8,
            LightAmount = 0.5,
        };

        using var sharpened = FineOutputSharpenStage.Apply(edge, options);

        // Sample a few pixels either side of the edge, away from the extraction-boundary artifacts.
        var darkSidePixel = ReadPixel(sharpened, halfWidth - 3, height / 2);
        var lightSidePixel = ReadPixel(sharpened, halfWidth + 2, height / 2);

        var undershoot = darkValue - darkSidePixel;
        var overshoot = lightSidePixel - lightValue;

        Assert.True(undershoot > 0, $"Expected a dark-side undershoot, got delta {undershoot}");
        Assert.True(overshoot > 0, $"Expected a light-side overshoot, got delta {overshoot}");
        Assert.True(undershoot > overshoot,
            $"Expected undershoot ({undershoot}) > overshoot ({overshoot}) since darkAmount > lightAmount");
    }

    [Fact]
    public void Apply_IsNoOp_WhenAmountIsZero()
    {
        using var edge = MakeStepEdge(20, 10, 50, 200);
        var options = new FineSharpenOptions { Amount = 0 };

        using var result = FineOutputSharpenStage.Apply(edge, options);

        Assert.Same(edge, result);
    }

    private static Image MakeStepEdge(int halfWidth, int height, double darkValue, double lightValue)
    {
        using var dark = (Image.Black(halfWidth, height) + darkValue).Cast(Enums.BandFormat.Float);
        using var light = (Image.Black(halfWidth, height) + lightValue).Cast(Enums.BandFormat.Float);
        using var gray = dark.Join(light, Enums.Direction.Horizontal);
        using var rgb = gray.Bandjoin(gray, gray);
        return rgb.Copy(interpretation: Enums.Interpretation.Srgb);
    }

    private static double ReadPixel(Image image, int x, int y)
    {
        var pixel = image.Getpoint(x, y);
        return pixel[0];
    }
}
