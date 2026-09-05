using AcuLume.Core.Sharpening;
using NetVips;

namespace AcuLume.Core.Tests;

public class EdgeMaskBuilderTests
{
    [Fact]
    public void BuildProtectionMask_IsHighAtEdge_AndLowInFlatAreas()
    {
        const int halfWidth = 40;
        const int height = 20;

        using var dark = (Image.Black(halfWidth, height) + 50).Cast(Enums.BandFormat.Float);
        using var light = (Image.Black(halfWidth, height) + 200).Cast(Enums.BandFormat.Float);
        using var luminance = dark.Join(light, Enums.Direction.Horizontal);

        var options = new EdgeProtectionOptions
        {
            Amount = 1.0,
            Threshold = 15.0,
            Softness = 10.0,
            DetectionBlur = 1.0,
        };

        using var mask = EdgeMaskBuilder.BuildProtectionMask(luminance, options);

        var atEdge = ReadPixel(mask, halfWidth, height / 2);
        var flatDark = ReadPixel(mask, 5, height / 2);
        var flatLight = ReadPixel(mask, halfWidth + halfWidth - 5, height / 2);

        Assert.True(atEdge > 0.9, $"Expected near-full protection at the edge, got {atEdge}");
        Assert.True(flatDark < 0.1, $"Expected near-zero protection in a flat area, got {flatDark}");
        Assert.True(flatLight < 0.1, $"Expected near-zero protection in a flat area, got {flatLight}");
    }

    private static double ReadPixel(Image image, int x, int y) => image.Getpoint(x, y)[0];
}
