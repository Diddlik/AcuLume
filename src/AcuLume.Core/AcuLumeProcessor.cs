using System.Diagnostics;
using AcuLume.Core.Imaging;
using AcuLume.Core.Sharpening;

namespace AcuLume.Core;

/// <summary>
/// Reusable processing entry point (spec section 8.1). The CLI is only a thin adapter around this type
/// so the same pipeline can later be embedded in other hosts (GUI, folder watcher, etc.).
/// </summary>
public sealed class AcuLumeProcessor
{
    public ProcessingResult Process(
        string inputPath,
        string outputPath,
        ProcessingOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(
            Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new AcuLumeProcessingException(
                inputPath, ProcessingStage.Validate, "Output path must not match the input path — source files are never overwritten.");
        }

        var stopwatch = Stopwatch.StartNew();

        using var image = Load(inputPath);
        cancellationToken.ThrowIfCancellationRequested();

        var inputWidth = image.Width;
        var inputHeight = image.Height;

        var processed = image;
        if (options.LongEdge is { } longEdge)
        {
            processed = Resize(inputPath, processed, longEdge, options.AllowUpscale);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (options.OutputSharpen.IsEnabled)
        {
            processed = Sharpen(inputPath, processed, options.OutputSharpen);
        }

        cancellationToken.ThrowIfCancellationRequested();
        Encode(inputPath, outputPath, processed, options.Quality);

        stopwatch.Stop();

        return new ProcessingResult
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            InputWidth = inputWidth,
            InputHeight = inputHeight,
            OutputWidth = processed.Width,
            OutputHeight = processed.Height,
            Elapsed = stopwatch.Elapsed,
        };
    }

    private static NetVips.Image Load(string inputPath)
    {
        try
        {
            return ImageLoader.LoadForProcessing(inputPath);
        }
        catch (FileNotFoundException ex)
        {
            throw new AcuLumeProcessingException(inputPath, ProcessingStage.Validate, ex.Message, ex);
        }
        catch (Exception ex)
        {
            throw new AcuLumeProcessingException(inputPath, ProcessingStage.Decode, $"Decode failed: {ex.Message}", ex);
        }
    }

    private static NetVips.Image Resize(string inputPath, NetVips.Image image, int longEdge, bool allowUpscale)
    {
        try
        {
            return ResizeEngine.ResizeToLongEdge(image, longEdge, allowUpscale);
        }
        catch (Exception ex)
        {
            throw new AcuLumeProcessingException(inputPath, ProcessingStage.Resize, $"Resize failed: {ex.Message}", ex);
        }
    }

    private static NetVips.Image Sharpen(string inputPath, NetVips.Image image, OutputSharpenOptions options)
    {
        try
        {
            return OutputSharpenStage.Apply(image, options);
        }
        catch (Exception ex)
        {
            throw new AcuLumeProcessingException(inputPath, ProcessingStage.Sharpen, $"Sharpen failed: {ex.Message}", ex);
        }
    }

    private static void Encode(string inputPath, string outputPath, NetVips.Image image, int quality)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ImageWriter.Save(image, outputPath, quality);
        }
        catch (NotSupportedException ex)
        {
            throw new AcuLumeProcessingException(inputPath, ProcessingStage.Encode, ex.Message, ex);
        }
        catch (Exception ex)
        {
            throw new AcuLumeProcessingException(inputPath, ProcessingStage.Encode, $"Encode failed: {ex.Message}", ex);
        }
    }
}
