using NetVips;

namespace AcuLume.Core.Sharpening;

/// <summary>
/// Capture sharpening (spec section 21): light restoration at capture resolution, before the resize.
/// Like the output stage it works on the L channel of a Lab conversion, so chroma is never touched.
/// </summary>
public static class CaptureSharpenStage
{
    public static Image Apply(Image rgb, CaptureSharpenOptions options)
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

        using var restored = options.Engine switch
        {
            CaptureSharpenEngine.RichardsonLucy => RichardsonLucy(luminance, options),
            _ => FineRestore(luminance, options),
        };

        using var restoredLab = restored.Bandjoin(a, b);
        return restoredLab.Colourspace(originalInterpretation);
    }

    /// <summary>
    /// The spec's conservative starting point: a plain fine-frequency high-pass added back at a low
    /// amount. Shares <see cref="FrequencyBandExtractor"/> with the output stage rather than
    /// restating the same maths.
    /// </summary>
    private static Image FineRestore(Image luminance, CaptureSharpenOptions options)
    {
        using var highPass = FrequencyBandExtractor.ExtractHighPass(luminance, options.Radius);
        using var scaled = highPass * options.Amount;
        return luminance + scaled;
    }

    /// <summary>Raises anything below <paramref name="floor"/> to it, so the RL division stays finite.</summary>
    private static Image ClampBelow(Image image, double floor) =>
        (image < floor).Ifthenelse(floor, image);

    /// <summary>
    /// Richardson-Lucy deconvolution against an assumed Gaussian point spread function. Each
    /// iteration is <c>f ← f · ((g / (f ⊛ h)) ⊛ h)</c>; the PSF is symmetric, so its mirror is
    /// itself and the correlation is another blur. Composed entirely from libvips ops — no managed
    /// per-pixel loop — and blended back by <see cref="CaptureSharpenOptions.Amount"/>, because full
    /// deconvolution is exactly the aggressive restoration the spec rules out.
    /// </summary>
    private static Image RichardsonLucy(Image luminance, CaptureSharpenOptions options)
    {
        // L is non-negative but can be zero; RL divides by the current estimate's blur.
        const double floor = 1e-4;

        using var observed = ClampBelow(luminance, floor);
        var estimate = observed.Copy();

        for (var i = 0; i < options.Iterations; i++)
        {
            using var blurred = estimate.Gaussblur(options.Radius, minAmpl: 0.005, precision: Enums.Precision.Float);
            using var safeBlur = ClampBelow(blurred, floor);
            using var ratio = observed / safeBlur;
            using var correction = ratio.Gaussblur(options.Radius, minAmpl: 0.005, precision: Enums.Precision.Float);
            var next = estimate * correction;
            estimate.Dispose();
            estimate = next;
        }

        using (estimate)
        {
            using var delta = estimate - luminance;
            using var scaled = delta * options.Amount;
            return luminance + scaled;
        }
    }
}
