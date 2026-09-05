using AcuLume.Cli;
using NetVips;

namespace AcuLume.Cli.Tests;

public class SharpenCommandTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "aculume-tests-" + Guid.NewGuid())).FullName;

    [Fact]
    public void Sharpen_ResizesToRequestedLongEdge()
    {
        var inputPath = Path.Combine(_tempDir, "input.jpg");
        var outputPath = Path.Combine(_tempDir, "output.jpg");
        CreateTestImage(inputPath, width: 400, height: 300);

        var exitCode = CliApp.Run(["sharpen", inputPath, "--long-edge", "200", "--output", outputPath]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outputPath));

        using var output = Image.NewFromFile(outputPath);
        Assert.Equal(200, output.Width);
        Assert.Equal(150, output.Height);
    }

    [Fact]
    public void Sharpen_RejectsOutputPathEqualToInput()
    {
        var inputPath = Path.Combine(_tempDir, "input.jpg");
        CreateTestImage(inputPath, width: 100, height: 100);

        var exitCode = CliApp.Run(["sharpen", inputPath, "--output", inputPath]);

        Assert.Equal((int)ExitCode.OutputConflict, exitCode);
    }

    [Fact]
    public void Sharpen_ReturnsInputNotFound_WhenSourceMissing()
    {
        var missingPath = Path.Combine(_tempDir, "missing.jpg");

        var exitCode = CliApp.Run(["sharpen", missingPath, "--long-edge", "100"]);

        Assert.Equal((int)ExitCode.InputNotFound, exitCode);
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
