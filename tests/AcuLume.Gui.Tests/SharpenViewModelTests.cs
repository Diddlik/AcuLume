using AcuLume.Core.Configuration;
using AcuLume.Core.Sharpening;
using AcuLume.Gui.Services;
using AcuLume.Gui.ViewModels;

namespace AcuLume.Gui.Tests;

public class SharpenViewModelTests
{
    /// <summary>
    /// Regression: "Full Resolution" used to be saved as a preset with the loaded photograph's own
    /// long edge, which made the preset specific to that image instead of meaning "do not resize".
    /// </summary>
    [Fact]
    public void ToPreset_WritesNoResize_ForFullResolution()
    {
        var viewModel = NewViewModel();
        viewModel.OutputTarget = OutputTarget.Full;
        viewModel.LongEdge = 6000;

        var preset = viewModel.ToPreset("full");

        Assert.NotNull(preset.Resize);
        Assert.False(preset.Resize!.Enabled);
        Assert.Null(preset.Resize.LongEdge);
    }

    [Fact]
    public void ToPreset_KeepsTheLongEdge_ForASizedTarget()
    {
        var viewModel = NewViewModel();
        viewModel.OutputTarget = OutputTarget.Custom;
        viewModel.LongEdge = 2400;
        viewModel.AllowUpscale = true;

        var preset = viewModel.ToPreset("web");

        Assert.True(preset.Resize!.Enabled);
        Assert.Equal(2400, preset.Resize.LongEdge);
        Assert.True(preset.Resize.AllowUpscale);
    }

    [Fact]
    public void ToPreset_OmitsCaptureSharpening_WhenOff()
    {
        var viewModel = NewViewModel();

        Assert.Null(viewModel.ToPreset("x").CaptureSharpen);
    }

    [Fact]
    public void CaptureSharpening_SurvivesASaveAndReloadRoundTrip()
    {
        var saved = NewViewModel();
        saved.CaptureLevel = CaptureSharpenLevel.Normal;
        saved.CaptureEngine = CaptureSharpenEngine.RichardsonLucy;
        saved.CaptureRadius = 1.2;
        saved.CaptureIterations = 6;

        var reloaded = NewViewModel();
        reloaded.Apply(saved.ToPreset("capture"));

        Assert.Equal(CaptureSharpenLevel.Normal, reloaded.CaptureLevel);
        Assert.Equal(CaptureSharpenEngine.RichardsonLucy, reloaded.CaptureEngine);
        Assert.Equal(1.2, reloaded.CaptureRadius);
        Assert.Equal(6, reloaded.CaptureIterations);
    }

    [Fact]
    public void BuildOptions_PassesCaptureSharpeningToTheEngine()
    {
        var viewModel = NewViewModel();
        viewModel.CaptureLevel = CaptureSharpenLevel.Low;
        viewModel.CaptureRadius = 0.9;

        var options = viewModel.BuildOptions();

        Assert.True(options.CaptureSharpen.IsEnabled);
        Assert.Equal(CaptureSharpenLevel.Low, options.CaptureSharpen.Level);
        Assert.Equal(0.9, options.CaptureSharpen.Radius);
    }

    /// <summary>
    /// Regression: a slider coerces a bound value into its own range and writes the coerced value
    /// back. When the presets were recalibrated upwards the fine amount slider still stopped at 2.0,
    /// so loading a preset of 6.0 silently became 2.0 and the GUI produced weaker output than the
    /// CLI for the same preset. Every built-in preset must fit inside what the panel can express.
    /// </summary>
    [Theory]
    [InlineData("web-1800-natural")]
    [InlineData("web-1800-crisp")]
    [InlineData("full-natural")]
    [InlineData("neutral")]
    public void PresetsFitTheSliderRanges(string name)
    {
        var preset = PresetLoader.Load(name);
        var options = PresetLoader.ToProcessingOptions(preset).OutputSharpen;

        Assert.InRange(options.Fine.Amount, 0, SharpenViewModel.MaxBandAmount);
        Assert.InRange(options.Medium.Amount, 0, SharpenViewModel.MaxBandAmount);
        Assert.InRange(options.Fine.Radius, BandSharpenOptions.MinimumRadius, SharpenViewModel.MaxBandRadius);
        Assert.InRange(options.Medium.Radius, BandSharpenOptions.MinimumRadius, SharpenViewModel.MaxBandRadius);
        Assert.InRange(options.Fine.DarkAmount, 0, SharpenViewModel.MaxDetailWeight);
        Assert.InRange(options.Fine.LightAmount, 0, SharpenViewModel.MaxDetailWeight);
    }

    private static SharpenViewModel NewViewModel() => new(new PresetLibrary(), new RecentImages());
}
