using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Applies independent dark/light detail weights (spec section 17 — the required distinguishing feature):
/// negative detail (darkening) and positive detail (lightening) are scaled separately.
/// </summary>
public static class AsymmetricDetailMixer
{
    public static Image Apply(Image detail, double darkAmount, double lightAmount)
    {
        using var isDark = detail < 0.0;
        using var darkened = detail * darkAmount;
        using var lightened = detail * lightAmount;
        return isDark.Ifthenelse(darkened, lightened);
    }
}
