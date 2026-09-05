using AcuLume.Core.Configuration;
using AcuLume.Core.Imaging;
using AcuLume.Core.Sharpening;

namespace AcuLume.Core.Comparison;

/// <summary>
/// Generates the comparison variants called for in spec section 43: original, resize-only, a
/// naive USM baseline (section 44), and the two built-in AcuLume presets — so the pipeline's
/// benefit over simple sharpening can be judged visually rather than assumed.
/// </summary>
public static class ComparisonSetBuilder
{
    public static IReadOnlyList<ComparisonVariant> Build(string inputPath, string outputDirectory, ComparisonSetOptions options)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}", inputPath);
        }

        Directory.CreateDirectory(outputDirectory);

        var variants = new List<ComparisonVariant>();

        var originalPath = Path.Combine(outputDirectory, $"01-original{Path.GetExtension(inputPath)}");
        File.Copy(inputPath, originalPath, overwrite: true);
        variants.Add(new ComparisonVariant("original", originalPath));

        var resizeOnlyPath = Path.Combine(outputDirectory, "02-resize-only.jpg");
        new AcuLumeProcessor().Process(
            inputPath, resizeOnlyPath, new ProcessingOptions { LongEdge = options.LongEdge, Quality = options.Quality });
        variants.Add(new ComparisonVariant("resize-only", resizeOnlyPath));

        var usmPath = Path.Combine(outputDirectory, "03-usm-baseline.jpg");
        BuildUsmBaseline(inputPath, usmPath, options);
        variants.Add(new ComparisonVariant("usm-baseline", usmPath));

        var naturalPath = Path.Combine(outputDirectory, "04-aculume-natural.jpg");
        BuildFromPreset(inputPath, naturalPath, "web-1800-natural", options);
        variants.Add(new ComparisonVariant("aculume-natural", naturalPath));

        var crispPath = Path.Combine(outputDirectory, "05-aculume-crisp.jpg");
        BuildFromPreset(inputPath, crispPath, "web-1800-crisp", options);
        variants.Add(new ComparisonVariant("aculume-crisp", crispPath));

        return variants;
    }

    private static void BuildUsmBaseline(string inputPath, string outputPath, ComparisonSetOptions options)
    {
        using var image = ImageLoader.LoadForProcessing(inputPath);
        var resized = ResizeEngine.ResizeToLongEdge(image, options.LongEdge, allowUpscale: false);
        using var sharpened = BaselineUnsharpMask.Apply(resized, options.UsmRadius, options.UsmAmount);
        ImageWriter.Save(sharpened, outputPath, options.Quality);
    }

    private static void BuildFromPreset(string inputPath, string outputPath, string presetName, ComparisonSetOptions options)
    {
        var processingOptions = PresetLoader.ToProcessingOptions(PresetLoader.Load(presetName)) with { Quality = options.Quality };
        new AcuLumeProcessor().Process(inputPath, outputPath, processingOptions);
    }
}
