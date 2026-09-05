using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Fine-frequency output sharpening (spec sections 13-17). Operates on the L channel of a Lab
/// conversion so chroma (a/b) is left untouched — this is what keeps sharpening from introducing
/// chromatic edge halos (spec section 14).
/// </summary>
public static class FineOutputSharpenStage
{
    public static Image Apply(Image rgb, FineSharpenOptions options)
    {
        if (!options.IsEnabled)
        {
            return rgb;
        }

        options.Validate();

        var originalInterpretation = rgb.Interpretation;

        using var lab = rgb.Colourspace(Enums.Interpretation.Lab);
        using var luminance = lab[0];
        using var a = lab[1];
        using var b = lab[2];

        using var highPass = FrequencyBandExtractor.ExtractHighPass(luminance, options.Radius);
        using var weighted = AsymmetricDetailMixer.Apply(highPass, options.DarkAmount, options.LightAmount);
        using var scaled = weighted * options.Amount;
        using var sharpenedLuminance = luminance + scaled;

        using var sharpenedLab = sharpenedLuminance.Bandjoin(a, b);
        return sharpenedLab.Colourspace(originalInterpretation);
    }
}
