using AcuLume.Core.Sharpening;
using NetVips;

namespace AcuLume.Core.Tests;

public class CaptureSharpenStageTests
{
    [Fact]
    public void Apply_ReturnsTheSameImage_WhenOff()
    {
        using var image = MakeBlurredEdge();

        var result = CaptureSharpenStage.Apply(image, new CaptureSharpenOptions());

        Assert.Same(image, result);
    }

    [Theory]
    [InlineData(CaptureSharpenEngine.FineRestore)]
    [InlineData(CaptureSharpenEngine.RichardsonLucy)]
    public void Apply_RecoversContrastAcrossABlurredEdge(CaptureSharpenEngine engine)
    {
        using var image = MakeBlurredEdge();
        var options = new CaptureSharpenOptions
        {
            Level = CaptureSharpenLevel.Normal,
            Engine = engine,
            Radius = 1.5,
        };

        using var restored = CaptureSharpenStage.Apply(image, options);

        // The edge transition must get steeper: sample either side of the blur's midpoint.
        Assert.True(EdgeSlope(restored) > EdgeSlope(image),
            $"{engine} did not steepen the edge ({EdgeSlope(image)} -> {EdgeSlope(restored)})");
    }

    [Theory]
    [InlineData(CaptureSharpenLevel.Low)]
    [InlineData(CaptureSharpenLevel.Normal)]
    public void Apply_ScalesWithLevel(CaptureSharpenLevel level)
    {
        using var image = MakeBlurredEdge();
        var options = new CaptureSharpenOptions { Level = level, Radius = 1.5 };

        using var restored = CaptureSharpenStage.Apply(image, options);

        Assert.True(EdgeSlope(restored) > EdgeSlope(image));
        Assert.True(options.Amount > 0);
    }

    [Fact]
    public void Validate_RejectsARadiusThatWouldDoNothing()
    {
        var options = new CaptureSharpenOptions { Level = CaptureSharpenLevel.Low, Radius = 0.1 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Validate_RejectsUnreasonableIterationCounts(int iterations)
    {
        var options = new CaptureSharpenOptions { Level = CaptureSharpenLevel.Low, Iterations = iterations };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>A step edge softened by a Gaussian — the thing capture sharpening claims to restore.</summary>
    private static Image MakeBlurredEdge()
    {
        using var dark = (Image.Black(40, 20) + 50).Cast(Enums.BandFormat.Float);
        using var light = (Image.Black(40, 20) + 200).Cast(Enums.BandFormat.Float);
        using var gray = dark.Join(light, Enums.Direction.Horizontal);
        using var blurred = gray.Gaussblur(1.5);
        using var rgb = blurred.Bandjoin(blurred, blurred);
        return rgb.Copy(interpretation: Enums.Interpretation.Srgb);
    }

    private static double EdgeSlope(Image image)
    {
        const int y = 10;
        return image.Getpoint(41, y)[0] - image.Getpoint(38, y)[0];
    }
}
