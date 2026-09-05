namespace AcuLume.Core.Configuration;

/// <summary>Thrown for a malformed preset. Presets are never silently clamped (spec section 31).</summary>
public sealed class PresetValidationException(string presetName, string message) : Exception(message)
{
    public string PresetName { get; } = presetName;
}
