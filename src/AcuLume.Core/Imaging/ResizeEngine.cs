using NetVips;

namespace AcuLume.Core.Imaging;

/// <summary>How the downscale is carried out (spec section 47, Phase 3 — experimental).</summary>
public enum ResizeStrategy
{
    /// <summary>One Lanczos 3 step straight to the target size. The default.</summary>
    Single,

    /// <summary>Repeated halving before the final step, so each stage low-passes the one after it.</summary>
    Staged,
}

/// <summary>Which encoding the resampling arithmetic happens in (spec section 47, Phase 3 — experimental).</summary>
public enum ResizeSpace
{
    /// <summary>Resample the gamma-encoded sRGB values directly. The default.</summary>
    Gamma,

    /// <summary>Convert to linear light, resample, convert back — physically correct averaging.</summary>
    LinearLight,
}

/// <summary>High-quality long-edge resize. Kept independent of any sharpening/output-size logic per architecture rules.</summary>
public static class ResizeEngine
{
    /// <summary>
    /// Computes the output dimensions for resizing so the longer edge equals <paramref name="longEdge"/>,
    /// preserving aspect ratio. Pure function, no image I/O, so it can be unit tested directly.
    /// </summary>
    public static (int Width, int Height) CalculateLongEdgeDimensions(
        int width, int height, int longEdge, bool allowUpscale)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Image dimensions must be positive.");
        }

        if (longEdge <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(longEdge), "Long edge must be positive.");
        }

        var currentLongEdge = Math.Max(width, height);
        var scale = longEdge / (double)currentLongEdge;

        if (scale >= 1.0 && !allowUpscale)
        {
            return (width, height);
        }

        var newWidth = Math.Max(1, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero));
        var newHeight = Math.Max(1, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero));
        return (newWidth, newHeight);
    }

    /// <summary>Resizes <paramref name="image"/> so its long edge equals <paramref name="longEdge"/>.</summary>
    public static Image ResizeToLongEdge(
        Image image,
        int longEdge,
        bool allowUpscale,
        ResizeStrategy strategy = ResizeStrategy.Single,
        ResizeSpace space = ResizeSpace.Gamma)
    {
        var (targetWidth, targetHeight) =
            CalculateLongEdgeDimensions(image.Width, image.Height, longEdge, allowUpscale);
        if (targetWidth == image.Width)
        {
            return image;
        }

        if (space == ResizeSpace.Gamma)
        {
            return Scale(image, targetWidth, targetHeight, strategy);
        }

        using var linear = image.Colourspace(Enums.Interpretation.Scrgb);
        using var resized = Scale(linear, targetWidth, targetHeight, strategy);
        return resized.Colourspace(image.Interpretation);
    }

    private static Image Scale(Image image, int targetWidth, int targetHeight, ResizeStrategy strategy)
    {
        if (strategy == ResizeStrategy.Single)
        {
            return image.Resize(targetWidth / (double)image.Width, kernel: Enums.Kernel.Lanczos3);
        }

        // Halve while more than a factor of two remains: each halving band-limits the input to the
        // next one, so detail beyond the following stage's Nyquist limit is filtered out rather than
        // folded back as aliasing.
        var current = image;
        var owned = false;

        while (targetWidth / (double)current.Width < 0.5)
        {
            var halved = current.Resize(0.5, kernel: Enums.Kernel.Lanczos3);
            if (owned)
            {
                current.Dispose();
            }

            current = halved;
            owned = true;
        }

        // Derive the last step from the target dimensions rather than a residual factor: chained
        // scales accumulate rounding, and the long edge has to land exactly on the requested size.
        var result = current.Resize(
            targetWidth / (double)current.Width,
            kernel: Enums.Kernel.Lanczos3,
            vscale: targetHeight / (double)current.Height);

        if (owned)
        {
            current.Dispose();
        }

        return result;
    }
}
