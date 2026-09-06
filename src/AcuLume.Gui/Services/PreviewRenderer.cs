using AcuLume.Core;
using AcuLume.Core.Imaging;
using AcuLume.Core.Sharpening;
using Avalonia.Media.Imaging;
using NetVips;

namespace AcuLume.Gui.Services;

public sealed record PreviewFrame(
    Bitmap Before,
    Bitmap After,
    int Width,
    int Height,
    TimeSpan Elapsed,
    IReadOnlyList<double> Histogram);

/// <summary>
/// Renders interactive preview frames from the same <see cref="AcuLume.Core"/> pipeline the CLI uses,
/// so what the panel shows is what an export produces.
///
/// The preview is rendered at the *output* resolution because sharpening is output-size dependent
/// (spec section 23) — previewing at screen size would show a different result than the export.
/// The decoded source and the unsharpened resize are cached so a slider drag only re-runs the
/// sharpening stage.
/// </summary>
public sealed class PreviewRenderer : IDisposable
{
    /// <summary>Bucket count for the preview histogram; must divide 256.</summary>
    public const int HistogramBuckets = 64;

    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _sourcePath;
    private Image? _source;

    private CaptureSharpenOptions? _capturedWith;
    private Image? _captured;

    private int? _resizedLongEdge;
    private Image? _resized;
    private Bitmap? _beforeBitmap;

    public async Task<PreviewFrame> RenderAsync(string path, ProcessingOptions options, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Render(path, options, ct), ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private PreviewFrame Render(string path, ProcessingOptions options, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();

        EnsureSource(path);
        ct.ThrowIfCancellationRequested();

        EnsureCaptured(options.CaptureSharpen);
        ct.ThrowIfCancellationRequested();

        EnsureResized(options.LongEdge, options.AllowUpscale);
        ct.ThrowIfCancellationRequested();

        var resized = _resized!;
        var sharpened = OutputSharpenStage.Apply(resized, options.OutputSharpen);
        try
        {
            ct.ThrowIfCancellationRequested();
            var after = ToBitmap(sharpened);
            var histogram = BuildLuminanceHistogram(sharpened);
            started.Stop();
            return new PreviewFrame(
                _beforeBitmap!, after, resized.Width, resized.Height, started.Elapsed, histogram);
        }
        finally
        {
            // Apply() returns its input unchanged when every band is disabled; that instance is cached.
            if (!ReferenceEquals(sharpened, resized))
            {
                sharpened.Dispose();
            }
        }
    }

    private void EnsureSource(string path)
    {
        if (_sourcePath == path && _source is not null)
        {
            return;
        }

        InvalidateCapture();
        _source?.Dispose();
        _source = ImageLoader.LoadForProcessing(path, Enums.Access.Random);
        _sourcePath = path;
    }

    /// <summary>
    /// Capture sharpening runs before the resize, so it is cached separately from it: changing an
    /// output slider must not re-run it, and changing it must invalidate the resize below.
    /// </summary>
    private void EnsureCaptured(CaptureSharpenOptions options)
    {
        if (_captured is not null && _capturedWith == options)
        {
            return;
        }

        InvalidateCapture();

        var source = _source!;
        var captured = CaptureSharpenStage.Apply(source, options);
        _captured = ReferenceEquals(captured, source) ? source : captured.CopyMemory();
        if (!ReferenceEquals(captured, source) && !ReferenceEquals(captured, _captured))
        {
            captured.Dispose();
        }

        _capturedWith = options;
    }

    private void EnsureResized(int? longEdge, bool allowUpscale)
    {
        if (_resized is not null && _resizedLongEdge == longEdge)
        {
            return;
        }

        InvalidateResize();

        var source = _captured!;
        var resized = longEdge is { } edge ? ResizeEngine.ResizeToLongEdge(source, edge, allowUpscale) : source;

        // Materialise once so repeated sharpen passes don't re-run the Lanczos resize per slider move.
        _resized = resized.CopyMemory();
        if (!ReferenceEquals(resized, source))
        {
            resized.Dispose();
        }

        _resizedLongEdge = longEdge;
        _beforeBitmap = ToBitmap(_resized);
    }

    private void InvalidateCapture()
    {
        if (_captured is not null && !ReferenceEquals(_captured, _source))
        {
            _captured.Dispose();
        }

        _captured = null;
        _capturedWith = null;
        InvalidateResize();
    }

    private void InvalidateResize()
    {
        if (_resized is not null && !ReferenceEquals(_resized, _source) && !ReferenceEquals(_resized, _captured))
        {
            _resized.Dispose();
        }

        _resized = null;
        _resizedLongEdge = null;

        // Not disposed: a previously returned frame may still be on screen. Let the finalizer reclaim it.
        _beforeBitmap = null;
    }

    /// <summary>
    /// Luminance distribution of the rendered result, in <see cref="HistogramBuckets"/> buckets scaled
    /// so the tallest bucket is 1. Drives the preview's histogram overlay, which is what tells you
    /// whether sharpening has pushed highlights into clipping.
    /// </summary>
    private static IReadOnlyList<double> BuildLuminanceHistogram(Image image)
    {
        using var srgb = image.Interpretation == Enums.Interpretation.Srgb
            ? image.Copy()
            : image.Colourspace(Enums.Interpretation.Srgb);
        using var luminance = srgb.Colourspace(Enums.Interpretation.Bw);
        using var counts = luminance.HistFind();

        var buckets = new double[HistogramBuckets];
        var perBucket = 256 / HistogramBuckets;

        for (var level = 0; level < 256; level++)
        {
            buckets[level / perBucket] += counts.Getpoint(level, 0)[0];
        }

        var peak = buckets.Max();
        if (peak <= 0)
        {
            return buckets;
        }

        for (var i = 0; i < buckets.Length; i++)
        {
            buckets[i] /= peak;
        }

        return buckets;
    }

    private static Bitmap ToBitmap(Image image)
    {
        var srgb = image.Interpretation == Enums.Interpretation.Srgb
            ? image
            : image.Colourspace(Enums.Interpretation.Srgb);
        try
        {
            var png = srgb.PngsaveBuffer(compression: 1);
            using var stream = new MemoryStream(png, writable: false);
            return new Bitmap(stream);
        }
        finally
        {
            if (!ReferenceEquals(srgb, image))
            {
                srgb.Dispose();
            }
        }
    }

    public void Dispose()
    {
        InvalidateCapture();
        _source?.Dispose();
        _source = null;
        _gate.Dispose();
    }
}
