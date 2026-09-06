using AcuLume.Gui.Services;

namespace AcuLume.Gui.ViewModels;

/// <summary>
/// Facts about how this build processes images, and the shortcuts the Sharpen view actually binds.
/// Everything here is read from the running engine rather than restated by hand, so it cannot drift.
/// </summary>
public sealed class SettingsViewModel(PresetLibrary library)
{
    public IReadOnlyList<InfoRow> Processing { get; } =
    [
        new("Engine", $"AcuLume {AppInfo.Version}"),
        new("Image library", $"libvips {NetVips.NetVips.Version(0)}.{NetVips.NetVips.Version(1)}.{NetVips.NetVips.Version(2)}"),
        new("Resize kernel", "Lanczos 3"),
        new("Sharpening space", "Lab, L channel only"),
        new("Precision", "32-bit float"),
        new("Determinism", "Same input + preset ⇒ same output"),
        new("Network access", "None — all processing is local"),
    ];

    public IReadOnlyList<InfoRow> Shortcuts { get; } =
    [
        new("Open image", "Ctrl+O"),
        new("Export", "Ctrl+E"),
        new("Toggle before / after", "Space"),
        new("Fit", "1"),
        new("100%", "2"),
        new("200%", "3"),
        new("Split view", "B"),
    ];

    public IReadOnlyList<InfoRow> Storage { get; } =
    [
        new("User presets", library.UserPresetDirectory),
        new("Comparison cache", Path.Combine(Path.GetTempPath(), "AcuLume", "compare")),
    ];
}
