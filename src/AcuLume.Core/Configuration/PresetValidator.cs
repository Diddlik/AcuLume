namespace AcuLume.Core.Configuration;

/// <summary>
/// Rejects malformed presets outright rather than silently clamping them (spec section 31):
/// negative radii, impossible dimensions, invalid strengths, invalid quality.
/// </summary>
public static class PresetValidator
{
    private const int SupportedSchemaVersion = 1;

    public static void Validate(SharpenPreset preset)
    {
        if (preset.Version != SupportedSchemaVersion)
        {
            throw Invalid(preset, $"Unsupported preset schema version {preset.Version} (expected {SupportedSchemaVersion}).");
        }

        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            throw Invalid(preset, "Preset name must not be empty.");
        }

        if (preset.Resize is { } resize && resize.LongEdge is { } longEdge && longEdge <= 0)
        {
            throw Invalid(preset, "resize.longEdge must be positive.");
        }

        if (preset.OutputSharpen is { } outputSharpen)
        {
            ValidateBand(preset, "fine", outputSharpen.Fine);
            ValidateBand(preset, "medium", outputSharpen.Medium);
            ValidateFraction(preset, "noiseProtection", outputSharpen.NoiseProtection);
            ValidateFraction(preset, "edgeProtection", outputSharpen.EdgeProtection);
            ValidateFraction(preset, "haloProtection", outputSharpen.HaloProtection);
        }

        if (preset.Output is { } output && output.Quality is < 1 or > 100)
        {
            throw Invalid(preset, "output.quality must be between 1 and 100.");
        }
    }

    private static void ValidateBand(SharpenPreset preset, string bandName, BandPresetOptions? band)
    {
        if (band is null)
        {
            return;
        }

        if (band.Radius <= 0)
        {
            throw Invalid(preset, $"outputSharpen.{bandName}.radius must be positive.");
        }

        if (band.Amount < 0)
        {
            throw Invalid(preset, $"outputSharpen.{bandName}.amount must not be negative.");
        }

        if (band.DarkAmount < 0)
        {
            throw Invalid(preset, $"outputSharpen.{bandName}.darkAmount must not be negative.");
        }

        if (band.LightAmount < 0)
        {
            throw Invalid(preset, $"outputSharpen.{bandName}.lightAmount must not be negative.");
        }
    }

    private static void ValidateFraction(SharpenPreset preset, string fieldName, double value)
    {
        if (value is < 0 or > 1)
        {
            throw Invalid(preset, $"outputSharpen.{fieldName} must be between 0 and 1.");
        }
    }

    private static PresetValidationException Invalid(SharpenPreset preset, string message) =>
        new(preset.Name, message);
}
