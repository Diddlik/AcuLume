namespace AcuLume.Core.Comparison;

/// <summary>
/// A fixed crop region, applied identically across every comparison variant (spec section 43).
/// These are geometric coordinates the caller supplies (e.g. picked once by eye around a strong
/// edge or fine detail in a specific test photo) — not content-detected automatically.
/// </summary>
public sealed record CropSpec(string Label, int X, int Y, int Width, int Height);
