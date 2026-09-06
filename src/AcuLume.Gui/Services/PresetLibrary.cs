using System.Text.Json;
using AcuLume.Core.Configuration;

namespace AcuLume.Gui.Services;

public sealed record PresetEntry(string Name, SharpenPreset Preset, string? FilePath)
{
    /// <summary>Built-in presets are embedded in the engine and cannot be edited or deleted.</summary>
    public bool IsBuiltIn => FilePath is null;
}

/// <summary>
/// The presets the GUI offers: the engine's embedded built-ins plus the user's own JSON presets.
/// User presets live next to the app's other roaming data so they survive reinstalls, and use the
/// same on-disk schema as the CLI's <c>--preset &lt;path&gt;</c>, so a preset saved here can be
/// passed straight to the CLI.
/// </summary>
public sealed class PresetLibrary
{
    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public PresetLibrary()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AcuLume", "presets"))
    {
    }

    public PresetLibrary(string userPresetDirectory) => UserPresetDirectory = userPresetDirectory;

    public string UserPresetDirectory { get; }

    /// <summary>Built-ins first, then user presets, each group alphabetical.</summary>
    public IReadOnlyList<PresetEntry> Load()
    {
        var entries = new List<PresetEntry>();

        foreach (var name in PresetLoader.BuiltInPresetNames)
        {
            entries.Add(new PresetEntry(name, PresetLoader.Load(name), FilePath: null));
        }

        if (!Directory.Exists(UserPresetDirectory))
        {
            return entries;
        }

        foreach (var file in Directory.EnumerateFiles(UserPresetDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            // One unreadable file must not hide the rest of the library.
            if (TryLoadFile(file) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static PresetEntry? TryLoadFile(string file)
    {
        try
        {
            var preset = PresetLoader.Load(file);
            return new PresetEntry(preset.Name, preset, file);
        }
        catch (Exception ex) when (ex is PresetValidationException or IOException)
        {
            return null;
        }
    }

    public string Save(SharpenPreset preset)
    {
        PresetValidator.Validate(preset);
        Directory.CreateDirectory(UserPresetDirectory);

        var path = Path.Combine(UserPresetDirectory, $"{SanitizeFileName(preset.Name)}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, WriteOptions));
        return path;
    }

    public static void Delete(PresetEntry entry)
    {
        if (entry.FilePath is { } path)
        {
            File.Delete(path);
        }
    }

    /// <summary>Copies an external preset file into the library, validating it first.</summary>
    public string Import(string sourcePath)
    {
        var preset = PresetLoader.Load(sourcePath);
        return Save(preset);
    }

    public static void Export(PresetEntry entry, string targetPath) =>
        File.WriteAllText(targetPath, JsonSerializer.Serialize(entry.Preset, WriteOptions));

    /// <summary>Makes a name that does not collide with an existing preset, e.g. "web copy 2".</summary>
    public string UniqueName(string baseName)
    {
        var taken = Load().Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} {i}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var sanitized = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        return sanitized.Trim().Length == 0 ? "preset" : sanitized.Trim();
    }
}
