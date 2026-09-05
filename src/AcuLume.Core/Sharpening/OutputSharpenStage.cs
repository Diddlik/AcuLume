using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Multi-band output sharpening (spec sections 13-17). Each band operates on the L channel of a
/// Lab conversion so chroma (a/b) is left untouched — this is what keeps sharpening from
/// introducing chromatic edge halos (spec section 14). Band contributions are summed before
/// being added back to L once.
/// </summary>
public static class OutputSharpenStage
{
    public static Image Apply(Image rgb, OutputSharpenOptions options)
    {
        if (!options.IsEnabled)
        {
            return rgb;
        }

        options.Fine.Validate();
        options.Medium.Validate();
        options.EdgeProtection.Validate();
        options.NoiseProtection.Validate();
        options.HaloLimiter.Validate();

        var originalInterpretation = rgb.Interpretation;

        using var lab = rgb.Colourspace(Enums.Interpretation.Lab);
        using var luminance = lab[0];
        using var a = lab[1];
        using var b = lab[2];

        using var rawContribution = BuildContribution(luminance, options);
        using var edgeProtected = ApplyEdgeProtection(luminance, rawContribution, options.EdgeProtection);
        using var contribution = HaloLimiter.Apply(luminance, edgeProtected, options.HaloLimiter);
        using var sharpenedLuminance = luminance + contribution;

        using var sharpenedLab = sharpenedLuminance.Bandjoin(a, b);
        return sharpenedLab.Colourspace(originalInterpretation);
    }

    private static Image BuildContribution(Image luminance, OutputSharpenOptions options)
    {
        Image? total = null;

        foreach (var band in new[] { options.Fine, options.Medium })
        {
            if (!band.IsEnabled)
            {
                continue;
            }

            using var highPass = FrequencyBandExtractor.ExtractHighPass(luminance, band.Radius);
            using var denoised = NoiseProtection.Apply(highPass, options.NoiseProtection);
            using var weighted = AsymmetricDetailMixer.Apply(denoised, band.DarkAmount, band.LightAmount);
            using var scaled = weighted * band.Amount;

            if (total is null)
            {
                total = scaled.Copy();
            }
            else
            {
                var next = total + scaled;
                total.Dispose();
                total = next;
            }
        }

        return total ?? throw new InvalidOperationException("BuildContribution called with no enabled bands.");
    }

    private static Image ApplyEdgeProtection(Image luminance, Image contribution, EdgeProtectionOptions options)
    {
        if (!options.IsEnabled)
        {
            return contribution.Copy();
        }

        using var mask = EdgeMaskBuilder.BuildProtectionMask(luminance, options);
        using var attenuation = 1.0 - (mask * options.Amount);
        return contribution * attenuation;
    }
}
