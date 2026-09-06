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
            Exif = ReadExif(image, fields),
        };
    }

    private static Dictionary<string, string> ReadExif(Image image, string[] fields)
    {
        var exif = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            // libvips names EXIF tags "exif-ifd<n>-<TagName>"; the IFD number is an encoding detail.
            if (!field.StartsWith("exif-ifd", StringComparison.Ordinal))
            {
                continue;
            }

            var separator = field.IndexOf('-', "exif-ifd".Length);
            if (separator < 0 || image.Get(field) is not string raw)
            {
                continue;
            }

            var tag = field[(separator + 1)..];
            exif.TryAdd(tag, StripTypeAnnotation(raw));
        }

        return exif;
    }

    /// <summary>
    /// libvips returns EXIF values as "0.008 sec (5/625, Rational, 1 components, 8 bytes)" — the
    /// trailing parenthesis is the tag's storage type, which is noise for a reader.
    /// </summary>
    private static string StripTypeAnnotation(string value)
    {
        var open = value.LastIndexOf(" (", StringComparison.Ordinal);
        return (open > 0 && value.EndsWith(')') ? value[..open] : value).Trim();
    }

    /// <summary>
    /// Loads an image for pixel processing, baking EXIF orientation into pixel data.
    /// Sequential access streams the file once (cheapest for a one-shot CLI run); random access
    /// keeps the decoded image re-readable, which interactive hosts need to re-render a preview
    /// from the same loaded source on every parameter change.
    /// </summary>
    public static Image LoadForProcessing(string path, Enums.Access access = Enums.Access.Sequential)
    {
        RequireExistingFile(path);

        using var raw = Image.NewFromFile(path, access: access);
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
