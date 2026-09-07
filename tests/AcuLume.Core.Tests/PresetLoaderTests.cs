using AcuLume.Core.Configuration;
using AcuLume.Core.Sharpening;

namespace AcuLume.Core.Tests;

public class PresetLoaderTests
{
    [Theory]
    [InlineData("full-natural")]
    [InlineData("web-1800-natural")]
    [InlineData("web-1800-crisp")]
    [InlineData("neutral")]
    public void Load_ReadsAllBuiltInPresets(string name)
    {
        var preset = PresetLoader.Load(name);

        Assert.Equal(name, preset.Name);
    }

    [Fact]
    public void BuiltInPresetNames_ListsAllFour()
    {
        Assert.Equal(
            new[] { "full-natural", "neutral", "web-1800-crisp", "web-1800-natural" },
            PresetLoader.BuiltInPresetNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Load_Throws_WhenPresetDoesNotExist()
    {
        Assert.Throws<FileNotFoundException>(() => PresetLoader.Load("does-not-exist"));
    }

    [Fact]
    public void Load_ReadsFromFilePath_WhenGivenOne()
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, """{"version":1,"name":"custom"}""");

            var preset = PresetLoader.Load(tempPath);

            Assert.Equal("custom", preset.Name);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void ToProcessingOptions_MapsWebPresetCorrectly()
    {
        var preset = PresetLoader.Load("web-1800-natural");

        var options = PresetLoader.ToProcessingOptions(preset);

        Assert.Equal(1800, options.LongEdge);
        Assert.False(options.AllowUpscale);
        Assert.Equal(90, options.Quality);
        Assert.Equal(0.35, options.OutputSharpen.Fine.Radius);
        Assert.Equal(13.44, options.OutputSharpen.Fine.Amount);
        Assert.Equal(0.65, options.OutputSharpen.EdgeProtection.Amount);
        Assert.Equal(1.00, options.OutputSharpen.NoiseProtection.Amount);
        Assert.Equal(0.70, options.OutputSharpen.HaloLimiter.Amount);
        Assert.True(options.Denoise.IsEnabled);
        Assert.Equal(DenoiseEngine.GuidedFilter, options.Denoise.Engine);
        Assert.Equal(2.00, options.Denoise.Threshold);
    }

    [Fact]
    public void ToProcessingOptions_LeavesResizeDisabled_ForFullNatural()
    {
        var preset = PresetLoader.Load("full-natural");

        var options = PresetLoader.ToProcessingOptions(preset);

        Assert.Null(options.LongEdge);
    }

    [Theory]
    [InlineData("""{"version":2,"name":"x"}""")]
    [InlineData("""{"version":1,"name":""}""")]
    [InlineData("""{"version":1,"name":"x","resize":{"longEdge":0}}""")]
    [InlineData("""{"version":1,"name":"x","outputSharpen":{"fine":{"radius":-1,"amount":1}}}""")]
    [InlineData("""{"version":1,"name":"x","outputSharpen":{"noiseProtection":1.5}}""")]
    [InlineData("""{"version":1,"name":"x","output":{"quality":0}}""")]
    public void Load_RejectsMalformedPresets(string json)
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, json);

            Assert.Throws<PresetValidationException>(() => PresetLoader.Load(tempPath));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
