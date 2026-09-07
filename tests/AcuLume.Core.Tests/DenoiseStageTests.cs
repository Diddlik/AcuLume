using AcuLume.Core.Sharpening;
using NetVips;

namespace AcuLume.Core.Tests;

public class DenoiseStageTests
{
    [Fact]
    public void Apply_ReturnsTheSameImage_WhenDisabled()
    {
        using var image = NoisyGradient();

        Assert.Same(image, DenoiseStage.Apply(image, new DenoiseOptions()));
    }

    /// <summary>Each engine has to actually lower the noise floor of a flat, grainy field.</summary>
    [Theory]
    [InlineData(DenoiseEngine.GuidedFilter)]
    [InlineData(DenoiseEngine.MultiScaleShrinkage)]
    [InlineData(DenoiseEngine.NonLocalMeans)]
    public void Apply_ReducesNoiseOnAFlatField(DenoiseEngine engine)
    {
        using var noisy = NoisyFlat(sigma: 3.0);
        var options = new DenoiseOptions { Engine = engine, Amount = 1.0, Radius = 2 };

        using var cleaned = DenoiseStage.Apply(noisy, options);

        var before = Roughness(noisy);
        var after = Roughness(cleaned);
        Assert.True(after < before * 0.8, $"{engine} left {after:0.0000} of {before:0.0000}");
    }

    /// <summary>
    /// The point of every one of these over a blur: a hard edge must survive. A Gaussian would pass
    /// the noise test above and fail this one.
    /// </summary>
    [Theory]
    [InlineData(DenoiseEngine.GuidedFilter)]
    [InlineData(DenoiseEngine.MultiScaleShrinkage)]
    [InlineData(DenoiseEngine.NonLocalMeans)]
    public void Apply_KeepsAStrongEdge(DenoiseEngine engine)
    {
        using var edge = NoisyEdge();
        var options = new DenoiseOptions { Engine = engine, Amount = 1.0, Radius = 2 };

        using var cleaned = DenoiseStage.Apply(edge, options);

        var before = EdgeStep(edge);
        var after = EdgeStep(cleaned);
        Assert.True(after > before * 0.85, $"{engine} softened the edge from {before:0.0} to {after:0.0}");
    }

    [Fact]
    public void Apply_ScalesWithAmount()
    {
        using var noisy = NoisyFlat(sigma: 3.0);
        var half = new DenoiseOptions { Engine = DenoiseEngine.GuidedFilter, Amount = 0.5, Radius = 2 };
        var full = half with { Amount = 1.0 };

        using var halfResult = DenoiseStage.Apply(noisy, half);
        using var fullResult = DenoiseStage.Apply(noisy, full);

        Assert.True(Roughness(fullResult) < Roughness(halfResult));
        Assert.True(Roughness(halfResult) < Roughness(noisy));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_RejectsAnAmountOutsideZeroToOne(double amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DenoiseOptions { Amount = amount }.Validate());
    }

    [Fact]
    public void Validate_RejectsAnUnreasonableRadius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DenoiseOptions { Amount = 1.0, Radius = 20 }.Validate());
    }

    private static Image NoisyFlat(double sigma)
    {
        using var noise = Image.Gaussnoise(256, 256, sigma: sigma, mean: 55, seed: 11);
        using var rgb = noise.Bandjoin(noise, noise);
        return rgb.Copy(interpretation: Enums.Interpretation.Srgb).Cast(Enums.BandFormat.Uchar);
    }

    private static Image NoisyGradient()
    {
        using var ramp = Image.Xyz(128, 128)[0].Cast(Enums.BandFormat.Float);
        using var rgb = ramp.Bandjoin(ramp, ramp);
        return rgb.Copy(interpretation: Enums.Interpretation.Srgb).Cast(Enums.BandFormat.Uchar);
    }

    private static Image NoisyEdge()
    {
        using var dark = Image.Gaussnoise(128, 256, sigma: 3.0, mean: 40, seed: 3);
        using var light = Image.Gaussnoise(128, 256, sigma: 3.0, mean: 200, seed: 4);
        using var gray = dark.Join(light, Enums.Direction.Horizontal);
        using var rgb = gray.Bandjoin(gray, gray);
        return rgb.Copy(interpretation: Enums.Interpretation.Srgb).Cast(Enums.BandFormat.Uchar);
    }

    /// <summary>Mean absolute gradient — how grainy the image is.</summary>
    private static double Roughness(Image image)
    {
        using var lab = image.Colourspace(Enums.Interpretation.Lab);
        using var l = lab[0];
        using var shifted = l.Crop(1, 0, l.Width - 1, l.Height);
        using var original = l.Crop(0, 0, l.Width - 1, l.Height);
        using var difference = (shifted - original).Abs();
        return difference.Avg();
    }

    /// <summary>Height of the step across the seam of <see cref="NoisyEdge"/>.</summary>
    private static double EdgeStep(Image image)
    {
        using var lab = image.Colourspace(Enums.Interpretation.Lab);
        using var l = lab[0];
        using var left = l.Crop(100, 0, 20, l.Height);
        using var right = l.Crop(136, 0, 20, l.Height);
        return right.Avg() - left.Avg();
    }
}
