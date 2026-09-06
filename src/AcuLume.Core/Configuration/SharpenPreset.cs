using System.Text.Json.Serialization;

namespace AcuLume.Core.Configuration;

/// <summary>
/// JSON-serializable preset schema (spec sections 24, 31-32). Deliberately smaller than the spec's
/// illustrative full schema: the granular protection thresholds stay CLI-only calibration knobs.
/// `captureSharpen` is optional, so files written before it existed still validate unchanged.
/// </summary>
public sealed record SharpenPreset
{
    /// <summary>Schema version — bump only when making a breaking change to this shape.</summary>
    public required int Version { get; init; }

    public required string Name { get; init; }

    /// <summary>Optional one-line summary shown in the GUI's preset library.</summary>
    public string? Description { get; init; }

    /// <summary>Marks a preset as not yet calibrated against a real validation set (spec coding rule 13).</summary>
    public bool Experimental { get; init; }

    public ResizePresetOptions? Resize { get; init; }

    public CaptureSharpenPresetOptions? CaptureSharpen { get; init; }

    public OutputSharpenPresetOptions? OutputSharpen { get; init; }

    public OutputPresetOptions? Output { get; init; }
}

public sealed record ResizePresetOptions
{
    public bool Enabled { get; init; } = true;

    public int? LongEdge { get; init; }

    public bool AllowUpscale { get; init; }
}

public sealed record CaptureSharpenPresetOptions
{
    /// <summary>"off", "low" or "normal" (spec section 21).</summary>
    public string Level { get; init; } = "off";

    /// <summary>"fineRestore" or "richardsonLucy".</summary>
    public string Engine { get; init; } = "fineRestore";

    public double Radius { get; init; } = 0.8;

    public int Iterations { get; init; } = 4;
}

public sealed record BandPresetOptions
{
    public required double Radius { get; init; }

    public required double Amount { get; init; }

    public double DarkAmount { get; init; } = 0.8;

    public double LightAmount { get; init; } = 0.5;
}

public sealed record OutputSharpenPresetOptions
{
    public BandPresetOptions? Fine { get; init; }

    public BandPresetOptions? Medium { get; init; }

    [JsonPropertyName("noiseProtection")]
    public double NoiseProtection { get; init; }

    [JsonPropertyName("edgeProtection")]
    public double EdgeProtection { get; init; }

    [JsonPropertyName("haloProtection")]
    public double HaloProtection { get; init; }
}

public sealed record OutputPresetOptions
{
    public int Quality { get; init; } = 90;
}
