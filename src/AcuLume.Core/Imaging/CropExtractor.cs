using NetVips;

namespace AcuLume.Core.Imaging;

/// <summary>Extracts a fixed rectangular crop, clamped to the image bounds.</summary>
public static class CropExtractor
{
    public static Image Extract(Image image, int x, int y, int width, int height)
    {
        var clampedX = Math.Clamp(x, 0, Math.Max(0, image.Width - 1));
        var clampedY = Math.Clamp(y, 0, Math.Max(0, image.Height - 1));
        var clampedWidth = Math.Clamp(width, 1, image.Width - clampedX);
        var clampedHeight = Math.Clamp(height, 1, image.Height - clampedY);
        return image.Crop(clampedX, clampedY, clampedWidth, clampedHeight);
    }
}
