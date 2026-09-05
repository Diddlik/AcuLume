using NetVips;

namespace AcuLume.Cli.Tests;

public class CompareCommandTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "aculume-compare-cli-tests-" + Guid.NewGuid())).FullName;

    [Fact]
    public void Compare_GeneratesVariantsAndCrops()
    {
        var inputPath = Path.Combine(_tempDir, "input.jpg");
        CreateTestImage(inputPath, 400, 300);
        var outDir = Path.Combine(_tempDir, "out");

        var exitCode = CliApp.Run(
            ["compare", inputPath, "--output", outDir, "--long-edge", "200", "--crop", "spot=10,10,50,50"]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(outDir, "02-resize-only.jpg")));
        Assert.True(File.Exists(Path.Combine(outDir, "crops", "spot", "resize-only.jpg")));
    }

    [Fact]
    public void Compare_ReturnsInvalidOptions_ForMalformedCrop()
    {
        var inputPath = Path.Combine(_tempDir, "input.jpg");
        CreateTestImage(inputPath, 100, 100);
        var outDir = Path.Combine(_tempDir, "out");

        var exitCode = CliApp.Run(["compare", inputPath, "--output", outDir, "--crop", "not-valid"]);

        Assert.Equal((int)ExitCode.InvalidOptions, exitCode);
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
