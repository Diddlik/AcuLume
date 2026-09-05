using NetVips;

namespace AcuLume.Core.Imaging;

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
    public static Image ResizeToLongEdge(Image image, int longEdge, bool allowUpscale)
    {
        var (targetWidth, _) = CalculateLongEdgeDimensions(image.Width, image.Height, longEdge, allowUpscale);
        if (targetWidth == image.Width)
        {
            return image;
        }

        var scale = targetWidth / (double)image.Width;
        return image.Resize(scale, kernel: Enums.Kernel.Lanczos3);
    }
}
