using NetVips;

namespace AcuLume.Core.Imaging;

public static class ImageWriter
{
    /// <summary>Encodes <paramref name="image"/> to <paramref name="path"/>, preserving EXIF/ICC metadata by default.</summary>
    public static void Save(Image image, string path, int quality)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();

        switch (extension)
        {
            case "jpg" or "jpeg":
                image.Jpegsave(path, q: quality, keep: Enums.ForeignKeep.All);
                break;
            case "png":
                image.Pngsave(path, keep: Enums.ForeignKeep.All);
                break;
            case "tif" or "tiff":
                image.Tiffsave(path, keep: Enums.ForeignKeep.All);
                break;
            default:
                throw new NotSupportedException($"Unsupported output format: .{extension}");
        }
    }
}
