namespace AcuLume.Core;

/// <summary>Wraps a processing failure with the file and pipeline stage it happened in (spec section 39).</summary>
public sealed class AcuLumeProcessingException(string filePath, ProcessingStage stage, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public string FilePath { get; } = filePath;
    public ProcessingStage Stage { get; } = stage;
}

public enum ProcessingStage
{
    Validate,
    Decode,
    Resize,
    Sharpen,
    Encode,
}
