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

    private static SharpenViewModel NewViewModel() => new(new PresetLibrary(), new RecentImages());
}
