using NetVips;

namespace AcuLume.Core.Imaging;

/// <summary>Decodes images via libvips and normalizes EXIF orientation before any further processing.</summary>
public static class ImageLoader
{
    public static ImageMetadata ReadMetadata(string path)
    {
        RequireExistingFile(path);

        using var image = Image.NewFromFile(path, access: Enums.Access.Sequential);

        var fields = image.GetFields();
        var orientation = fields.Contains("orientation") ? (int)image.Get("orientation") : 1;
        var hasIccProfile = fields.Contains("icc-profile-data");
        var loader = fields.Contains("vips-loader") ? (string)image.Get("vips-loader") : "unknown";

        return new ImageMetadata
        {
            Path = path,
            Width = image.Width,
            Height = image.Height,
            Bands = image.Bands,
            HasAlpha = image.HasAlpha(),
            Loader = loader,
            Orientation = orientation,
            HasIccProfile = hasIccProfile,
        };
    }

    /// <summary>Loads an image for pixel processing, baking EXIF orientation into pixel data.</summary>
    public static Image LoadForProcessing(string path)
    {
        RequireExistingFile(path);

        using var raw = Image.NewFromFile(path, access: Enums.Access.Sequential);
        return raw.Autorot();
    }

    private static void RequireExistingFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Input file not found: {path}", path);
        }
    }
}
