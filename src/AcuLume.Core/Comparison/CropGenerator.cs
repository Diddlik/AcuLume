using AcuLume.Core.Imaging;
using NetVips;

namespace AcuLume.Core.Comparison;

/// <summary>Re-extracts the same crop region from every comparison variant (spec section 43).</summary>
public static class CropGenerator
{
    public static IReadOnlyList<string> Generate(
        IReadOnlyList<ComparisonVariant> variants, IReadOnlyList<CropSpec> crops, string outputDirectory)
    {
        var generated = new List<string>();

        foreach (var crop in crops)
        {
            var cropDirectory = Path.Combine(outputDirectory, "crops", crop.Label);
            Directory.CreateDirectory(cropDirectory);

            foreach (var variant in variants)
            {
                using var image = Image.NewFromFile(variant.Path);
                using var cropped = CropExtractor.Extract(image, crop.X, crop.Y, crop.Width, crop.Height);

                var cropPath = Path.Combine(cropDirectory, $"{variant.Name}.jpg");
                ImageWriter.Save(cropped, cropPath, quality: 95);
                generated.Add(cropPath);
            }
        }

        return generated;
    }
}
