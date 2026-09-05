namespace AcuLume.Core;

public sealed record ProcessingResult
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public required int InputWidth { get; init; }
    public required int InputHeight { get; init; }
    public required int OutputWidth { get; init; }
    public required int OutputHeight { get; init; }
    public required TimeSpan Elapsed { get; init; }
}
