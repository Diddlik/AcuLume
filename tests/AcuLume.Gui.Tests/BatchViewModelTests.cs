using AcuLume.Gui.Services;
using AcuLume.Gui.ViewModels;
using NetVips;

namespace AcuLume.Gui.Tests;

public class BatchViewModelTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "aculume-batch-tests-" + Guid.NewGuid())).FullName;

    [Fact]
    public async Task RunAsync_ProcessesSelectedImages_IntoOutputFolder()
    {
        var inputDir = Directory.CreateDirectory(Path.Combine(_tempDir, "in")).FullName;
        var outputDir = Path.Combine(_tempDir, "out");
        CreateTestImage(Path.Combine(inputDir, "a.jpg"), 400, 300);
        CreateTestImage(Path.Combine(inputDir, "b.jpg"), 400, 300);

        var viewModel = NewViewModel();
        viewModel.AddFolder(inputDir);
        viewModel.OutputFolder = outputDir;
        viewModel.SelectedPreset = "web-1800-natural";

        Assert.Equal(2, viewModel.Items.Count);
        Assert.True(viewModel.CanRun);

        await viewModel.RunAsync();

        Assert.All(viewModel.Items, item => Assert.Equal(BatchStatus.Done, item.Status));
        Assert.Equal(2, viewModel.Completed);
        Assert.True(File.Exists(Path.Combine(outputDir, "a.aculume.jpg")));
        Assert.True(File.Exists(Path.Combine(outputDir, "b.aculume.jpg")));
    }

    [Fact]
    public async Task RunAsync_SkipsUnselectedImages()
    {
        var inputDir = Directory.CreateDirectory(Path.Combine(_tempDir, "in")).FullName;
        var outputDir = Path.Combine(_tempDir, "out");
        CreateTestImage(Path.Combine(inputDir, "keep.jpg"), 200, 200);
        CreateTestImage(Path.Combine(inputDir, "skip.jpg"), 200, 200);

        var viewModel = NewViewModel();
        viewModel.AddFolder(inputDir);
        viewModel.OutputFolder = outputDir;
        viewModel.Items.Single(i => i.FileName == "skip.jpg").IsSelected = false;

        Assert.Equal(1, viewModel.SelectedCount);

        await viewModel.RunAsync();

        Assert.True(File.Exists(Path.Combine(outputDir, "keep.aculume.jpg")));
        Assert.False(File.Exists(Path.Combine(outputDir, "skip.aculume.jpg")));
    }

    [Fact]
    public void AddFiles_IgnoresDuplicatesAndUnsupportedExtensions()
    {
        var inputDir = Directory.CreateDirectory(Path.Combine(_tempDir, "in")).FullName;
        var image = Path.Combine(inputDir, "a.jpg");
        CreateTestImage(image, 100, 100);
        var notAnImage = Path.Combine(inputDir, "notes.txt");
        File.WriteAllText(notAnImage, "text");

        var viewModel = NewViewModel();
        viewModel.AddFiles([image, image, notAnImage]);

        Assert.Single(viewModel.Items);
        Assert.Equal("100 × 100", viewModel.Items[0].Resolution);
    }

    /// <summary>Points the library at an empty directory so the tests only see the built-in presets.</summary>
    private BatchViewModel NewViewModel() =>
        new(new PresetLibrary(Path.Combine(_tempDir, "presets")));

    private static void CreateTestImage(string path, int width, int height)
    {
        using var image = Image.Black(width, height, bands: 3) + 128;
        image.Jpegsave(path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
