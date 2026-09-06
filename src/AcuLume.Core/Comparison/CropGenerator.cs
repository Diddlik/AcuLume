using AcuLume.Core.Imaging;
using NetVips;

namespace AcuLume.Core.Comparison;

/// <summary>Re-extracts the same crop region from every comparison variant (spec section 43).</summary>
public static class CropGenerator
{
    /// <summary>
    /// Crop coordinates are given in the first variant's pixel space — the untouched original, the
    /// image a photographer picks the region from. Downscaled variants are cropped through the same
    /// scale factor they were resized by, so every crop shows the same subject; they differ in pixel
    /// count, which is precisely what the comparison is about.
    /// </summary>
    public static IReadOnlyList<string> Generate(
        IReadOnlyList<ComparisonVariant> variants, IReadOnlyList<CropSpec> crops, string outputDirectory)
    {
        var generated = new List<string>();
        if (variants.Count == 0)
        {
            return generated;
        }

        using var reference = Image.NewFromFile(variants[0].Path);
        var referenceWidth = reference.Width;

        foreach (var crop in crops)
        {
            var cropDirectory = Path.Combine(outputDirectory, "crops", crop.Label);
            Directory.CreateDirectory(cropDirectory);

            foreach (var variant in variants)
            {
                using var image = Image.NewFromFile(variant.Path);
                var scale = image.Width / (double)referenceWidth;
                using var cropped = CropExtractor.Extract(
                    image,
                    (int)Math.Round(crop.X * scale),
                    (int)Math.Round(crop.Y * scale),
                    (int)Math.Round(crop.Width * scale),
                    (int)Math.Round(crop.Height * scale));

                var cropPath = Path.Combine(cropDirectory, $"{variant.Name}.jpg");
                ImageWriter.Save(cropped, cropPath, quality: 95);
                generated.Add(cropPath);
            }
        }

        return generated;
    }
}
