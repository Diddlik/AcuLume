using AcuLume.Core.Sharpening;
using NetVips;

namespace AcuLume.Core.Tests;

public class OutputSharpenStageTests
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

        var options = new OutputSharpenOptions
        {
            Fine = new BandSharpenOptions { Radius = 2.0, Amount = 1.0, DarkAmount = 0.8, LightAmount = 0.5 },
        };

        using var sharpened = OutputSharpenStage.Apply(edge, options);

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
    public void Apply_IsNoOp_WhenNoBandIsEnabled()
    {
        using var edge = MakeStepEdge(20, 10, 50, 200);
        var options = new OutputSharpenOptions();

        using var result = OutputSharpenStage.Apply(edge, options);

        Assert.Same(edge, result);
    }

    [Fact]
    public void Apply_MediumBandAloneAlsoProducesLargerUndershootThanOvershoot()
    {
        const int halfWidth = 40;
        const int height = 20;
        const double darkValue = 50;
        const double lightValue = 200;

        using var edge = MakeStepEdge(halfWidth, height, darkValue, lightValue);

        var options = new OutputSharpenOptions
        {
            Medium = new BandSharpenOptions { Radius = 2.0, Amount = 1.0, DarkAmount = 0.8, LightAmount = 0.5 },
        };

        using var sharpened = OutputSharpenStage.Apply(edge, options);

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
    public void Apply_CombinesFineAndMediumContributions()
    {
        const int halfWidth = 40;
        const int height = 20;
        const double darkValue = 50;
        const double lightValue = 200;

        using var edge = MakeStepEdge(halfWidth, height, darkValue, lightValue);

        var band = new BandSharpenOptions { Radius = 2.0, Amount = 1.0, DarkAmount = 0.8, LightAmount = 0.5 };
        var fineOnly = new OutputSharpenOptions { Fine = band };
        var fineAndMedium = fineOnly with { Medium = band };

        using var fineOnlyResult = OutputSharpenStage.Apply(edge, fineOnly);
        using var combinedResult = OutputSharpenStage.Apply(edge, fineAndMedium);

        var fineOnlyValue = ReadPixel(fineOnlyResult, halfWidth - 3, height / 2);
        var combinedValue = ReadPixel(combinedResult, halfWidth - 3, height / 2);

        // Two identical bands stacked must sharpen roughly twice as hard as one.
        var fineOnlyUndershoot = darkValue - fineOnlyValue;
        var combinedUndershoot = darkValue - combinedValue;
        Assert.True(combinedUndershoot > fineOnlyUndershoot * 1.5,
            $"Expected stacked bands to sharpen substantially more (fine-only delta {fineOnlyUndershoot}, combined delta {combinedUndershoot})");
    }

    [Fact]
    public void Apply_EdgeProtectionReducesSharpeningNearStrongEdge()
    {
        const int halfWidth = 40;
        const int height = 20;
        const double darkValue = 50;
        const double lightValue = 200;

        using var edge = MakeStepEdge(halfWidth, height, darkValue, lightValue);

        var band = new BandSharpenOptions { Radius = 2.0, Amount = 1.0, DarkAmount = 0.8, LightAmount = 0.5 };
        var withoutProtection = new OutputSharpenOptions { Fine = band };
        var withProtection = withoutProtection with
        {
            EdgeProtection = new EdgeProtectionOptions
            {
                Amount = 1.0,
                Threshold = 10.0,
                Softness = 8.0,
                DetectionBlur = 3.0,
            },
        };

        using var unprotectedResult = OutputSharpenStage.Apply(edge, withoutProtection);
        using var protectedResult = OutputSharpenStage.Apply(edge, withProtection);

        var unprotectedUndershoot = darkValue - ReadPixel(unprotectedResult, halfWidth - 3, height / 2);
        var protectedUndershoot = darkValue - ReadPixel(protectedResult, halfWidth - 3, height / 2);

        Assert.True(protectedUndershoot < unprotectedUndershoot,
            $"Expected edge protection to reduce the undershoot (unprotected {unprotectedUndershoot}, protected {protectedUndershoot})");
    }

    [Fact]
    public void Apply_HaloLimiterReducesOvershootBeyondLocalContrast()
    {
        const int halfWidth = 40;
        const int height = 20;
        const double darkValue = 50;
        const double lightValue = 200;

        using var edge = MakeStepEdge(halfWidth, height, darkValue, lightValue);

        // A large, aggressively-scaled fine band to produce an overshoot much larger than the
        // local contrast range should reasonably allow.
        var band = new BandSharpenOptions { Radius = 2.0, Amount = 5.0, DarkAmount = 0.8, LightAmount = 0.5 };
        var withoutLimiter = new OutputSharpenOptions { Fine = band };
        var withLimiter = withoutLimiter with
        {
            // A small window means a point a few px into the (otherwise flat) light region sees
            // ~0 local range, so any overshoot the wider fine band still produces there gets
            // fully suppressed while the unlimited version keeps it.
            HaloLimiter = new HaloLimiterOptions { Amount = 1.0, WindowRadius = 1.0, DarkLimit = 0.5, LightLimit = 0.3 },
        };

        using var unlimitedResult = OutputSharpenStage.Apply(edge, withoutLimiter);
        using var limitedResult = OutputSharpenStage.Apply(edge, withLimiter);

        var samplePoint = halfWidth + 2;
        var unlimitedOvershoot = ReadPixel(unlimitedResult, samplePoint, height / 2) - lightValue;
        var limitedOvershoot = ReadPixel(limitedResult, samplePoint, height / 2) - lightValue;

        Assert.True(limitedOvershoot < unlimitedOvershoot,
            $"Expected the halo limiter to reduce the overshoot (unlimited {unlimitedOvershoot}, limited {limitedOvershoot})");
    }

    /// <summary>
    /// Synthetic noise field (spec section 41.2 / Task 7): low-amplitude random luminance
    /// variation should barely be sharpened once noise protection is enabled, while it is
    /// amplified freely when disabled.
    /// </summary>
    [Fact]
    public void Apply_NoiseProtectionSuppressesSharpeningOfWeakRandomVariation()
    {
        using var noise = MakeNoiseField(width: 64, height: 64, sigma: 0.4, mean: 128, seed: 42);

        var band = new BandSharpenOptions { Radius = 1.0, Amount = 1.0, DarkAmount = 0.8, LightAmount = 0.5 };
        var withoutProtection = new OutputSharpenOptions { Fine = band };
        var withProtection = withoutProtection with
        {
            NoiseProtection = new NoiseProtectionOptions { Amount = 1.0, Threshold = 2.0, Softness = 2.0 },
        };

        using var unprotectedResult = OutputSharpenStage.Apply(noise, withoutProtection);
        using var protectedResult = OutputSharpenStage.Apply(noise, withProtection);

        // Compare against the Lab round-trip alone (no sharpening) rather than the raw input:
        // colourspace conversion has its own float rounding error that otherwise swamps the
        // much smaller sharpening delta we actually want to measure.
        using var lab = noise.Colourspace(Enums.Interpretation.Lab);
        using var roundTripBaseline = lab.Colourspace(noise.Interpretation);

        using var unprotectedDelta = (unprotectedResult - roundTripBaseline).Abs();
        using var protectedDelta = (protectedResult - roundTripBaseline).Abs();

        var unprotectedMeanDelta = unprotectedDelta.Avg();
        var protectedMeanDelta = protectedDelta.Avg();

        Assert.True(protectedMeanDelta < unprotectedMeanDelta * 0.5,
            $"Expected noise protection to substantially reduce sharpening of noise " +
            $"(unprotected mean delta {unprotectedMeanDelta}, protected mean delta {protectedMeanDelta})");
    }

    private static Image MakeNoiseField(int width, int height, double sigma, double mean, int seed)
    {
        using var noise = Image.Gaussnoise(width, height, sigma: sigma, mean: mean, seed: seed);
        using var rgb = noise.Bandjoin(noise, noise);
        return rgb.Copy(interpretation: Enums.Interpretation.Srgb);
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
