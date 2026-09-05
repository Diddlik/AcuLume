using AcuLume.Core.Sharpening;
using NetVips;

namespace AcuLume.Core.Tests;

public class NoiseProtectionTests
{
    [Fact]
    public void Apply_SuppressesSmallMagnitudeDetail_WhenEnabled()
    {
        using var detail = MakeConstant(0.3);
        var options = new NoiseProtectionOptions { Amount = 1.0, Threshold = 2.0, Softness = 2.0 };

        using var result = NoiseProtection.Apply(detail, options);

        Assert.Equal(0.0, ReadPixel(result), 3);
    }

    [Fact]
    public void Apply_PreservesLargeMagnitudeDetail_WhenEnabled()
    {
        using var detail = MakeConstant(10.0);
        var options = new NoiseProtectionOptions { Amount = 1.0, Threshold = 2.0, Softness = 2.0 };

        using var result = NoiseProtection.Apply(detail, options);

        Assert.Equal(10.0, ReadPixel(result), 3);
    }

    [Fact]
    public void Apply_IsNoOp_WhenAmountIsZero()
    {
        using var detail = MakeConstant(0.3);
        var options = new NoiseProtectionOptions { Amount = 0 };

        using var result = NoiseProtection.Apply(detail, options);

        Assert.Equal(0.3, ReadPixel(result), 3);
    }

    private static Image MakeConstant(double value) => (Image.Black(4, 4) + value).Cast(Enums.BandFormat.Float);

    private static double ReadPixel(Image image) => image.Getpoint(0, 0)[0];
}
