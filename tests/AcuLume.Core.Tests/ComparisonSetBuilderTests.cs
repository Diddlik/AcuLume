using AcuLume.Core.Comparison;
using NetVips;

namespace AcuLume.Core.Tests;

public class ComparisonSetBuilderTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "aculume-compare-tests-" + Guid.NewGuid())).FullName;

    [Fact]
    public void Build_GeneratesAllFiveVariants_AtRequestedLongEdge()
    {
        var inputPath = Path.Combine(_tempDir, "input.jpg");
        CreateTestImage(inputPath, width: 400, height: 300);

        var variants = ComparisonSetBuilder.Build(inputPath, _tempDir, new ComparisonSetOptions { LongEdge = 200 });

        Assert.Equal(
            new[] { "original", "resize-only", "usm-baseline", "aculume-natural", "aculume-crisp" },
            variants.Select(v => v.Name));

        foreach (var variant in variants)
        {
            Assert.True(File.Exists(variant.Path), $"Expected {variant.Path} to exist");
        }

        using var resizeOnly = Image.NewFromFile(variants.Single(v => v.Name == "resize-only").Path);
        Assert.Equal(200, resizeOnly.Width);

        using var usm = Image.NewFromFile(variants.Single(v => v.Name == "usm-baseline").Path);
        Assert.Equal(200, usm.Width);
    }

    [Fact]
    public void Build_PreservesOriginalExactly()
    {
        var inputPath = Path.Combine(_tempDir, "input.jpg");
        CreateTestImage(inputPath, width: 100, height: 100);

        var variants = ComparisonSetBuilder.Build(inputPath, _tempDir, new ComparisonSetOptions());

        var originalVariant = variants.Single(v => v.Name == "original");
        Assert.Equal(File.ReadAllBytes(inputPath), File.ReadAllBytes(originalVariant.Path));
    }

    [Fact]
    public void Build_Throws_WhenInputMissing()
    {
        Assert.Throws<FileNotFoundException>(
            () => ComparisonSetBuilder.Build(Path.Combine(_tempDir, "missing.jpg"), _tempDir, new ComparisonSetOptions()));
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
