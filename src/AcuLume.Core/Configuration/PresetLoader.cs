using System.Text.Json;
using AcuLume.Core.Sharpening;

namespace AcuLume.Core.Configuration;

/// <summary>
/// Loads presets by built-in name (embedded from the repository's top-level <c>presets/</c>
/// directory) or by file path, and converts them into <see cref="ProcessingOptions"/>.
/// </summary>
public static class PresetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string> BuiltInPresetNames { get; } =
        typeof(PresetLoader).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("Presets.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n => n["Presets.".Length..^".json".Length])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Loads and validates a preset, either a built-in name or a path to a JSON file.</summary>
    public static SharpenPreset Load(string nameOrPath)
    {
        var json = File.Exists(nameOrPath) ? File.ReadAllText(nameOrPath) : ReadEmbedded(nameOrPath);

        SharpenPreset? preset;
        try
        {
            preset = JsonSerializer.Deserialize<SharpenPreset>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new PresetValidationException(nameOrPath, $"Invalid preset JSON: {ex.Message}");
        }

        if (preset is null)
        {
            throw new PresetValidationException(nameOrPath, "Preset file is empty.");
        }

        PresetValidator.Validate(preset);
        return preset;
    }

    public static ProcessingOptions ToProcessingOptions(SharpenPreset preset)
    {
        var options = new ProcessingOptions();

        if (preset.Resize is { Enabled: true } resize)
        {
            options = options with { LongEdge = resize.LongEdge, AllowUpscale = resize.AllowUpscale };
        }

        if (preset.OutputSharpen is { } sharpen)
        {
            options = options with
            {
                OutputSharpen = new OutputSharpenOptions
                {
                    Fine = ToBand(sharpen.Fine) ?? new BandSharpenOptions { Radius = 0.6, Amount = 0 },
                    Medium = ToBand(sharpen.Medium) ?? new BandSharpenOptions { Radius = 1.4, Amount = 0 },
                    EdgeProtection = new EdgeProtectionOptions { Amount = sharpen.EdgeProtection },
                    NoiseProtection = new NoiseProtectionOptions { Amount = sharpen.NoiseProtection },
                    HaloLimiter = new HaloLimiterOptions { Amount = sharpen.HaloProtection },
                },
            };
        }

        if (preset.Output is { } output)
        {
            options = options with { Quality = output.Quality };
        }

        return options;
    }

    private static BandSharpenOptions? ToBand(BandPresetOptions? band) => band is null
        ? null
        : new BandSharpenOptions
        {
            Radius = band.Radius,
            Amount = band.Amount,
            DarkAmount = band.DarkAmount,
            LightAmount = band.LightAmount,
        };

    private static string ReadEmbedded(string name)
    {
        var assembly = typeof(PresetLoader).Assembly;
        var resourceName = $"Presets.{name}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Preset not found: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
