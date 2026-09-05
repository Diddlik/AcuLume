using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// A classic, naive unsharp mask applied independently to every RGB channel (spec section 44):
/// no luminance isolation, no dark/light split, no edge/noise/halo protection. Exists purely as
/// a comparison baseline to demonstrate AcuLume's pipeline provides a visible benefit over
/// "amount / radius / threshold" sharpening — it is not part of the AcuLume algorithm itself.
/// </summary>
public static class BaselineUnsharpMask
{
    public static Image Apply(Image rgb, double radius, double amount)
    {
        using var blurred = rgb.Gaussblur(radius);
        using var detail = rgb - blurred;
        using var scaled = detail * amount;
        return rgb + scaled;
    }
}
