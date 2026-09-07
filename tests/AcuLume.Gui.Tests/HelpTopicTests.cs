using AcuLume.Gui.ViewModels;

namespace AcuLume.Gui.Tests;

public class HelpTopicTests
{
    /// <summary>
    /// The strips fail silently: Avalonia swallows binding errors, so a missing image is an empty
    /// box and nothing else. Both halves of every topic the panel shows must be present.
    /// </summary>
    [Theory]
    [InlineData("fine")]
    [InlineData("medium")]
    [InlineData("balance")]
    [InlineData("noise")]
    [InlineData("edge")]
    [InlineData("halo")]
    [InlineData("capture")]
    [InlineData("denoise")]
    public void HelpImagesExist(string key)
    {
        var dir = Path.Combine(RepositoryRoot(), "src", "AcuLume.Gui", "Assets", "help");

        Assert.True(File.Exists(Path.Combine(dir, $"{key}-before.png")), $"{key}-before.png is missing");
        Assert.True(File.Exists(Path.Combine(dir, $"{key}-after.png")), $"{key}-after.png is missing");
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AcuLume.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
