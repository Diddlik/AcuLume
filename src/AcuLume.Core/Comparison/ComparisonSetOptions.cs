namespace AcuLume.Core.Comparison;

/// <summary>Settings for <see cref="ComparisonSetBuilder"/> (spec section 43-44).</summary>
public sealed record ComparisonSetOptions
{
    public int LongEdge { get; init; } = 1800;

    /// <summary>Gaussian sigma (px) for the naive USM baseline.</summary>
    public double UsmRadius { get; init; } = 1.0;

    /// <summary>Strength for the naive USM baseline.</summary>
    public double UsmAmount { get; init; } = 0.5;

    public int Quality { get; init; } = 90;
}
