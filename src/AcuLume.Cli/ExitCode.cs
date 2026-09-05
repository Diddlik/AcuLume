namespace AcuLume.Cli;

/// <summary>Stable exit codes for automation (spec section 28).</summary>
public enum ExitCode
{
    Success = 0,
    GeneralError = 1,
    InvalidOptions = 2,
    InputNotFound = 3,
    UnsupportedFormat = 4,
    InvalidPreset = 5,
    OutputConflict = 6,
    Cancelled = 7,
}
