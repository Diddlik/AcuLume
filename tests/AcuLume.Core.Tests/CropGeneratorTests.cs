using AcuLume.Core.Comparison;
using NetVips;

namespace AcuLume.Core.Tests;

public class CropGeneratorTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "aculume-crop-tests-" + Guid.NewGuid())).FullName;

    [Fact]
    public void Generate_WritesOneCropPerVariantPerLabel()
    {
        var variantPath1 = Path.Combine(_tempDir, "v1.jpg");
        var variantPath2 = Path.Combine(_tempDir, "v2.jpg");
        CreateTestImage(variantPath1, 200, 200);
        CreateTestImage(variantPath2, 200, 200);

        var variants = new[]
        {
            new ComparisonVariant("v1", variantPath1),
            new ComparisonVariant("v2", variantPath2),
        };
        var crops = new[] { new CropSpec("center", 50, 50, 60, 60) };

        var generated = CropGenerator.Generate(variants, crops, _tempDir);

        Assert.Equal(2, generated.Count);
        foreach (var path in generated)
        {
            Assert.True(File.Exists(path));
            using var crop = Image.NewFromFile(path);
            Assert.Equal(60, crop.Width);
            Assert.Equal(60, crop.Height);
        }
    }

    [Fact]
    public void Generate_ClampsCropToImageBounds()
    {
        var variantPath = Path.Combine(_tempDir, "v1.jpg");
        CreateTestImage(variantPath, 100, 100);

        var variants = new[] { new ComparisonVariant("v1", variantPath) };
        var crops = new[] { new CropSpec("oversized", 80, 80, 100, 100) };

        var generated = CropGenerator.Generate(variants, crops, _tempDir);

        using var crop = Image.NewFromFile(generated.Single());
        Assert.Equal(20, crop.Width);
        Assert.Equal(20, crop.Height);
    }

    private static void CreateTestImage(string path, int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3) + 128;
        image.Jpegsave(path);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }
}
