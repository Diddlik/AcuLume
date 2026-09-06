using System.Text.Json;

namespace AcuLume.Gui.Services;

public sealed record RecentImage(string Path, string Name, string Meta);

/// <summary>
/// The last few images opened in Sharpen, persisted next to the user's presets so the list
/// survives a restart. Entries whose file has since disappeared are dropped on load.
/// </summary>
public sealed class RecentImages
{
    private const int MaxEntries = 8;

    private readonly string _file;

    public RecentImages()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AcuLume", "recent.json"))
    {
    }

    public RecentImages(string file) => _file = file;

    public IReadOnlyList<RecentImage> Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                return [];
            }

            var entries = JsonSerializer.Deserialize<List<RecentImage>>(File.ReadAllText(_file)) ?? [];
            return entries.Where(e => File.Exists(e.Path)).ToList();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    /// <summary>Moves <paramref name="entry"/> to the front, and returns the trimmed list.</summary>
    public IReadOnlyList<RecentImage> Add(RecentImage entry)
    {
        var entries = new List<RecentImage> { entry };
        entries.AddRange(Load().Where(e => !string.Equals(e.Path, entry.Path, StringComparison.OrdinalIgnoreCase)));

        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        Save(entries);
        return entries;
    }

    private void Save(List<RecentImage> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(entries));
        }
        catch (IOException)
        {
            // A missing recent list is a cosmetic loss; never fail an image open over it.
        }
    }
}
