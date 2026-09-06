using System.Collections.ObjectModel;
using AcuLume.Core;
using AcuLume.Core.Configuration;
using AcuLume.Core.Imaging;
using AcuLume.Core.Sharpening;
using AcuLume.Gui.Services;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcuLume.Gui.ViewModels;

public enum PreviewMode { Before, After, Split }

public enum ZoomMode { Fit, Percent100, Percent200 }

public enum OutputTarget { Full, Web, Custom }

public sealed partial class SharpenViewModel : ObservableObject, IDisposable
{
    private const int WebLongEdge = 1800;

    // Long enough that a drag doesn't queue a render per pixel, short enough to feel live.
    private static readonly TimeSpan PreviewDebounce = TimeSpan.FromMilliseconds(180);

    private readonly PreviewRenderer _renderer = new();
    private readonly DispatcherTimer _debounce;
    private readonly AcuLumeProcessor _processor = new();
    private readonly PresetLibrary _library;
    private readonly RecentImages _recent;

    private CancellationTokenSource? _renderCts;
    private bool _suppressPreview;

    public SharpenViewModel(PresetLibrary library, RecentImages recent)
    {
        _library = library;
        _recent = recent;

        foreach (var entry in _recent.Load())
        {
            Recent.Add(entry);
        }

        _debounce = new DispatcherTimer { Interval = PreviewDebounce };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            _ = RenderPreviewAsync();
        };

        RefreshPresets();

        // Setting the property applies the preset through OnSelectedPresetChanged.
        SelectedPreset = Presets.Contains("web-1800-natural") ? "web-1800-natural" : Presets.FirstOrDefault();
    }

    /// <summary>Re-reads the library, keeping the current selection when it still exists.</summary>
    public void RefreshPresets()
    {
        var previous = SelectedPreset;

        Presets.Clear();
        foreach (var entry in _library.Load())
        {
            Presets.Add(entry.Name);
        }

        if (previous is not null && Presets.Contains(previous))
        {
            SelectedPreset = previous;
        }
    }

    // ---- image ----

    [ObservableProperty]
    public partial string? ImagePath { get; set; }

    [ObservableProperty]
    public partial string FileName { get; set; } = "No image loaded";

    [ObservableProperty]
    public partial int SourceWidth { get; set; }

    [ObservableProperty]
    public partial int SourceHeight { get; set; }

    [ObservableProperty]
    public partial int OutputWidth { get; set; }

    [ObservableProperty]
    public partial int OutputHeight { get; set; }

    [ObservableProperty]
    public partial Bitmap? BeforeImage { get; set; }

    [ObservableProperty]
    public partial Bitmap? AfterImage { get; set; }

    [ObservableProperty]
    public partial bool IsRendering { get; set; }

    /// <summary>Normalised luminance buckets of the current preview; drives the histogram overlay.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<double> Histogram { get; set; } = [];

    [ObservableProperty]
    public partial bool ShowHistogram { get; set; }

    [ObservableProperty]
    public partial bool IsExporting { get; set; }

    /// <summary>Set once an export succeeds, so the panel can offer to reveal the file.</summary>
    [ObservableProperty]
    public partial string? LastExportPath { get; set; }

    public bool HasExport => LastExportPath is not null;

    partial void OnLastExportPathChanged(string? value) => OnPropertyChanged(nameof(HasExport));

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Open an image to begin";

    public bool HasImage => ImagePath is not null;

    // ---- view state ----

    [ObservableProperty]
    public partial PreviewMode PreviewMode { get; set; } = PreviewMode.After;

    /// <summary>Split handle position as a fraction of the preview width.</summary>
    [ObservableProperty]
    public partial double SplitPosition { get; set; } = 0.5;

    [ObservableProperty]
    public partial ZoomMode Zoom { get; set; } = ZoomMode.Fit;

    public bool IsSplit => PreviewMode == PreviewMode.Split;

    public bool ShowBeforeLayer => PreviewMode != PreviewMode.After;

    /// <summary>Preview canvas size in device pixels; NaN lets the layout size it to fit.</summary>
    public double PreviewPixelWidth => Zoom == ZoomMode.Fit ? double.NaN : OutputWidth * ZoomFactor;

    public double PreviewPixelHeight => Zoom == ZoomMode.Fit ? double.NaN : OutputHeight * ZoomFactor;

    public string ZoomLabel => Zoom switch
    {
        ZoomMode.Percent100 => "100%",
        ZoomMode.Percent200 => "200%",
        _ => "Fit",
    };

    private double ZoomFactor => Zoom == ZoomMode.Percent200 ? 2 : 1;

    partial void OnPreviewModeChanged(PreviewMode value)
    {
        OnPropertyChanged(nameof(IsSplit));
        OnPropertyChanged(nameof(ShowBeforeLayer));
    }

    partial void OnZoomChanged(ZoomMode value)
    {
        OnPropertyChanged(nameof(ZoomLabel));
        OnPropertyChanged(nameof(PreviewPixelWidth));
        OnPropertyChanged(nameof(PreviewPixelHeight));
    }

    partial void OnOutputWidthChanged(int value) => OnPropertyChanged(nameof(PreviewPixelWidth));

    partial void OnOutputHeightChanged(int value) => OnPropertyChanged(nameof(PreviewPixelHeight));

    // ---- output ----

    [ObservableProperty]
    public partial OutputTarget OutputTarget { get; set; } = OutputTarget.Web;

    [ObservableProperty]
    public partial int LongEdge { get; set; } = WebLongEdge;

    [ObservableProperty]
    public partial int Quality { get; set; } = 90;

    // ---- sharpening ----

    /// <summary>Master multiplier over both band amounts; the panel's single "Overall" control.</summary>
    [ObservableProperty]
    public partial double Overall { get; set; } = 1.0;

    [ObservableProperty]
    public partial double FineAmount { get; set; }

    [ObservableProperty]
    public partial double FineRadius { get; set; } = 0.6;

    [ObservableProperty]
    public partial double MediumAmount { get; set; }

    [ObservableProperty]
    public partial double MediumRadius { get; set; } = 1.4;

    [ObservableProperty]
    public partial double DarkDetail { get; set; } = 0.8;

    [ObservableProperty]
    public partial double LightDetail { get; set; } = 0.5;

    // ---- protection ----

    [ObservableProperty]
    public partial bool NoiseProtectionEnabled { get; set; }

    [ObservableProperty]
    public partial double NoiseProtectionAmount { get; set; }

    [ObservableProperty]
    public partial bool EdgeProtectionEnabled { get; set; }

    [ObservableProperty]
    public partial double EdgeProtectionAmount { get; set; }

    [ObservableProperty]
    public partial bool HaloProtectionEnabled { get; set; }

    [ObservableProperty]
    public partial double HaloProtectionAmount { get; set; }

    // ---- presets ----

    public ObservableCollection<string> Presets { get; } = [];

    /// <summary>Most recently opened images, newest first.</summary>
    public ObservableCollection<RecentImage> Recent { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCaptureEnabled))]
    [NotifyPropertyChangedFor(nameof(CaptureHint))]
    public partial CaptureSharpenLevel CaptureLevel { get; set; } = CaptureSharpenLevel.Off;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRichardsonLucy))]
    [NotifyPropertyChangedFor(nameof(CaptureHint))]
    public partial CaptureSharpenEngine CaptureEngine { get; set; } = CaptureSharpenEngine.FineRestore;

    public bool IsCaptureEnabled => CaptureLevel != CaptureSharpenLevel.Off;

    public bool IsRichardsonLucy => CaptureEngine == CaptureSharpenEngine.RichardsonLucy;

    /// <summary>
    /// Capture sharpening measured worse than simply raising the output amounts (see
    /// docs/ALGORITHM.md). The panel says so rather than presenting it as a free improvement.
    /// </summary>
    public string CaptureHint => IsRichardsonLucy
        ? "Experimental deconvolution against an assumed Gaussian blur. Slow, and it needs a PSF a processed JPEG cannot supply — measured worse than simply raising the output amounts."
        : "Restores fine detail before the resize. Measured worse per unit of sharpening than raising the output amounts; off is the calibrated default.";

    [ObservableProperty]
    public partial double CaptureRadius { get; set; } = 0.8;

    [ObservableProperty]
    public partial int CaptureIterations { get; set; } = 4;

    [ObservableProperty]
    public partial bool AllowUpscale { get; set; }

    [ObservableProperty]
    public partial string? SelectedPreset { get; set; }

    /// <summary>
    /// Slider bounds live here rather than in the XAML because a slider silently coerces a bound
    /// value into its range: when the presets were recalibrated upwards, the panel clamped a fine
    /// amount of 6.0 down to its old maximum of 2.0 and wrote that back, so the GUI quietly produced
    /// weaker output than the CLI for the same preset. `PresetsFitTheSliderRanges` guards it.
    /// </summary>
    public static double MaxBandAmount => 12.0;

    public static double MaxBandRadius => 4.0;

    public static double MaxDetailWeight => 2.0;

    public static double MaxOverall => 2.0;

    /// <summary>
    /// One expandable before/after strip per section of the panel. Two of these are shown at their
    /// real preset value; the rest had to be exaggerated to be visible at all, which each one says.
    /// </summary>
    public HelpTopic CaptureHelp { get; } = new(
        "capture",
        "Restores detail at full resolution, before the resize. Most of what it recovers sits above the output's resolution and is thrown away by the downscale, which is why it is off by default.");

    public HelpTopic FineHelp { get; } = new(
        "fine",
        "The finest band: pore, thread and hair scale. It carries most of the crispness — and most of the risk of amplifying grain.");

    public HelpTopic MediumHelp { get; } = new(
        "medium",
        "The coarser band: shapes and structure rather than texture. It adds solidity without the crunchy edge of a single strong radius.",
        exaggerated: false);

    public HelpTopic BalanceHelp { get; } = new(
        "balance",
        "Scales the darkening and lightening halves of the detail separately. Bright halos are the ones the eye catches, so light detail is held below dark.");

    public HelpTopic NoiseHelp { get; } = new(
        "noise",
        "Suppresses detail below the grain floor instead of amplifying it. At full strength almost no noise survives into the output.");

    public HelpTopic EdgeHelp { get; } = new(
        "edge",
        "Attenuates sharpening along strong edges, where halos appear first.",
        exaggerated: false);

    public HelpTopic HaloHelp { get; } = new(
        "halo",
        "Caps overshoot at a fraction of the local contrast range. At the preset's limits the band contribution rarely reaches them, so it seldom engages; this pair uses much tighter limits.");

    partial void OnSelectedPresetChanged(string? value)
    {
        if (value is not null)
        {
            ApplySelectedPreset();
        }
    }

    partial void OnOutputTargetChanged(OutputTarget value)
    {
        LongEdge = value switch
        {
            OutputTarget.Full => Math.Max(SourceWidth, SourceHeight) is var edge and > 0 ? edge : LongEdge,
            OutputTarget.Web => WebLongEdge,
            _ => LongEdge,
        };
    }

    // ---- commands ----

    public async Task LoadImageAsync(string path)
    {
        var metadata = ImageLoader.ReadMetadata(path);

        // EXIF orientation is baked in at load time, so a rotated source reports swapped dimensions.
        var (width, height) = metadata.Orientation is >= 5 and <= 8
            ? (metadata.Height, metadata.Width)
            : (metadata.Width, metadata.Height);

        SourceWidth = width;
        SourceHeight = height;
        ImagePath = path;
        FileName = Path.GetFileName(path);
        LastExportPath = null;
        OnPropertyChanged(nameof(HasImage));

        Recent.Clear();
        foreach (var item in _recent.Add(new RecentImage(path, FileName, $"{width} × {height}")))
        {
            Recent.Add(item);
        }

        if (OutputTarget == OutputTarget.Full)
        {
            LongEdge = Math.Max(width, height);
        }

        await RenderPreviewAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void ResetPreset() => ApplySelectedPreset();

    public string SuggestedOutputName => ImagePath is null
        ? "output.jpg"
        : $"{Path.GetFileNameWithoutExtension(ImagePath)}.aculume.jpg";

    public async Task ExportAsync(string outputPath)
    {
        if (ImagePath is null)
        {
            return;
        }

        var options = BuildOptions();
        IsExporting = true;
        LastExportPath = null;
        StatusText = $"Exporting {Path.GetFileName(outputPath)}…";
        try
        {
            var input = ImagePath;
            var result = await Task.Run(() => _processor.Process(input, outputPath, options)).ConfigureAwait(true);
            LastExportPath = result.OutputPath;
            StatusText = $"Exported {result.OutputWidth} × {result.OutputHeight} in {result.Elapsed.TotalSeconds:F1} s";
        }
        catch (AcuLumeProcessingException ex)
        {
            StatusText = $"Export failed ({ex.Stage}): {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    /// <summary>Opens the exported file's folder in the system file manager and selects it.</summary>
    [RelayCommand]
    private void RevealExport()
    {
        if (LastExportPath is not { } path || !File.Exists(path))
        {
            return;
        }

        var (command, arguments) = OperatingSystem.IsWindows()
            ? ("explorer.exe", $"/select,\"{path}\"")
            : ("xdg-open", $"\"{Path.GetDirectoryName(path)}\"");

        try
        {
            using var process = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(command, arguments) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            StatusText = $"Could not open the folder: {ex.Message}";
        }
    }

    /// <summary>Saves the panel's current settings as a new user preset and selects it.</summary>
    public void SaveAsPreset(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var unique = _library.UniqueName(name.Trim());
        _library.Save(ToPreset(unique, "Saved from the Sharpen panel."));

        _suppressPreview = true;
        try
        {
            RefreshPresets();
            SelectedPreset = unique;
        }
        finally
        {
            _suppressPreview = false;
        }

        StatusText = $"Saved preset '{unique}'";
    }

    /// <summary>Name to offer when saving: the selected preset with a "copy" suffix.</summary>
    public string SuggestedPresetName => SelectedPreset is { } name ? $"{name} copy" : "My preset";

    public ProcessingOptions BuildOptions() => new()
    {
        LongEdge = LongEdge,
        AllowUpscale = AllowUpscale,
        Quality = Quality,
        CaptureSharpen = new CaptureSharpenOptions
        {
            Level = CaptureLevel,
            Engine = CaptureEngine,
            Radius = CaptureRadius,
            Iterations = CaptureIterations,
        },
        OutputSharpen = new OutputSharpenOptions
        {
            Fine = new BandSharpenOptions
            {
                Radius = FineRadius,
                Amount = FineAmount * Overall,
                DarkAmount = DarkDetail,
                LightAmount = LightDetail,
            },
            Medium = new BandSharpenOptions
            {
                Radius = MediumRadius,
                Amount = MediumAmount * Overall,
                DarkAmount = DarkDetail,
                LightAmount = LightDetail,
            },
            NoiseProtection = new NoiseProtectionOptions
            {
                Amount = NoiseProtectionEnabled ? NoiseProtectionAmount : 0,
            },
            EdgeProtection = new EdgeProtectionOptions
            {
                Amount = EdgeProtectionEnabled ? EdgeProtectionAmount : 0,
            },
            HaloLimiter = new HaloLimiterOptions
            {
                Amount = HaloProtectionEnabled ? HaloProtectionAmount : 0,
            },
        },
    };

    /// <summary>
    /// Captures the panel's current state as a savable preset. The Overall multiplier is folded into
    /// the band amounts, because the preset schema has no such field — the CLI must produce the same
    /// result from this file as the panel shows.
    /// </summary>
    public SharpenPreset ToPreset(string name, string? description = null) => new()
    {
        Version = 1,
        Name = name,
        Description = description,
        // "Full Resolution" means no resize at all. Writing the current image's long edge would
        // bake this photograph's dimensions into a preset meant to be reusable.
        Resize = OutputTarget == OutputTarget.Full
            ? new ResizePresetOptions { Enabled = false }
            : new ResizePresetOptions { Enabled = true, LongEdge = LongEdge, AllowUpscale = AllowUpscale },
        CaptureSharpen = CaptureLevel == CaptureSharpenLevel.Off
            ? null
            : new CaptureSharpenPresetOptions
            {
                Level = CaptureLevel.ToString(),
                Engine = CaptureEngine.ToString(),
                Radius = CaptureRadius,
                Iterations = CaptureIterations,
            },
        OutputSharpen = new OutputSharpenPresetOptions
        {
            Fine = new BandPresetOptions
            {
                Radius = FineRadius,
                Amount = FineAmount * Overall,
                DarkAmount = DarkDetail,
                LightAmount = LightDetail,
            },
            Medium = new BandPresetOptions
            {
                Radius = MediumRadius,
                Amount = MediumAmount * Overall,
                DarkAmount = DarkDetail,
                LightAmount = LightDetail,
            },
            NoiseProtection = NoiseProtectionEnabled ? NoiseProtectionAmount : 0,
            EdgeProtection = EdgeProtectionEnabled ? EdgeProtectionAmount : 0,
            HaloProtection = HaloProtectionEnabled ? HaloProtectionAmount : 0,
        },
        Output = new OutputPresetOptions { Quality = Quality },
    };

    private void ApplySelectedPreset()
    {
        if (SelectedPreset is { } name && _library.Load().FirstOrDefault(e => e.Name == name) is { } entry)
        {
            Apply(entry.Preset);
        }
    }

    /// <summary>Loads a preset's parameters into the panel. Used by the preset library and compare views.</summary>
    public void Apply(SharpenPreset preset)
    {
        var options = PresetLoader.ToProcessingOptions(preset);
        var sharpen = options.OutputSharpen;

        _suppressPreview = true;
        try
        {
            Overall = 1.0;
            FineAmount = sharpen.Fine.Amount;
            FineRadius = sharpen.Fine.Radius;
            MediumAmount = sharpen.Medium.Amount;
            MediumRadius = sharpen.Medium.Radius;
            DarkDetail = sharpen.Fine.DarkAmount;
            LightDetail = sharpen.Fine.LightAmount;

            NoiseProtectionAmount = sharpen.NoiseProtection.Amount;
            NoiseProtectionEnabled = sharpen.NoiseProtection.IsEnabled;
            EdgeProtectionAmount = sharpen.EdgeProtection.Amount;
            EdgeProtectionEnabled = sharpen.EdgeProtection.IsEnabled;
            HaloProtectionAmount = sharpen.HaloLimiter.Amount;
            HaloProtectionEnabled = sharpen.HaloLimiter.IsEnabled;

            var capture = options.CaptureSharpen;
            CaptureLevel = capture.Level;
            CaptureEngine = capture.Engine;
            CaptureRadius = capture.Radius;
            CaptureIterations = capture.Iterations;

            Quality = options.Quality;
            AllowUpscale = options.AllowUpscale;

            if (options.LongEdge is { } edge)
            {
                LongEdge = edge;
                OutputTarget = edge == WebLongEdge ? OutputTarget.Web : OutputTarget.Custom;
            }
            else
            {
                OutputTarget = OutputTarget.Full;
                LongEdge = Math.Max(SourceWidth, SourceHeight) is var full and > 0 ? full : LongEdge;
            }
        }
        finally
        {
            _suppressPreview = false;
        }

        SchedulePreview();
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (_suppressPreview || e.PropertyName is null || !AffectsPreview(e.PropertyName))
        {
            return;
        }

        SchedulePreview();
    }

    private static bool AffectsPreview(string propertyName) => propertyName is
        nameof(LongEdge) or nameof(Overall) or
        nameof(FineAmount) or nameof(FineRadius) or
        nameof(MediumAmount) or nameof(MediumRadius) or
        nameof(DarkDetail) or nameof(LightDetail) or
        nameof(NoiseProtectionEnabled) or nameof(NoiseProtectionAmount) or
        nameof(EdgeProtectionEnabled) or nameof(EdgeProtectionAmount) or
        nameof(HaloProtectionEnabled) or nameof(HaloProtectionAmount) or
        nameof(AllowUpscale) or
        nameof(CaptureLevel) or nameof(CaptureEngine) or
        nameof(CaptureRadius) or nameof(CaptureIterations);

    private void SchedulePreview()
    {
        if (ImagePath is null)
        {
            return;
        }

        _debounce.Stop();
        _debounce.Start();
    }

    private async Task RenderPreviewAsync()
    {
        if (ImagePath is not { } path)
        {
            return;
        }

        var previous = _renderCts;
        var cts = new CancellationTokenSource();
        _renderCts = cts;
        if (previous is not null)
        {
            await previous.CancelAsync().ConfigureAwait(true);
            previous.Dispose();
        }

        IsRendering = true;
        try
        {
            var frame = await _renderer.RenderAsync(path, BuildOptions(), cts.Token).ConfigureAwait(true);
            BeforeImage = frame.Before;
            AfterImage = frame.After;
            Histogram = frame.Histogram;
            OutputWidth = frame.Width;
            OutputHeight = frame.Height;
            StatusText = $"Preview {frame.Width} × {frame.Height} · {frame.Elapsed.TotalMilliseconds:F0} ms";
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer render; the newer one reports status.
            return;
        }
        catch (Exception ex)
        {
            StatusText = $"Preview failed: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_renderCts, cts))
            {
                IsRendering = false;
            }
        }
    }

    public void Dispose()
    {
        _debounce.Stop();
        _renderCts?.Dispose();
        _renderer.Dispose();
    }
}
