using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcuLume.Gui.ViewModels;

/// <summary>
/// The before/after strip a Sharpen section expands. Images are generated from the real engine by
/// build/generate-help-images.ps1, never drawn by hand, so they cannot drift away from what the
/// pipeline actually does.
///
/// Most settings move a 150 px crop by only one to three levels out of 255 at their calibrated
/// values. Those are rendered with the setting pushed well past its preset and say so, because two
/// identical-looking images would teach the opposite of the truth.
/// </summary>
public sealed partial class HelpTopic(string key, string explanation, bool exaggerated = true) : ObservableObject
{
    public string Explanation { get; } = explanation;

    /// <summary>Whether the pair was rendered past the preset value to make the effect visible.</summary>
    public bool IsExaggerated { get; } = exaggerated;

    // Image.Source needs an IImage; a binding to a string is not converted the way a XAML literal
    // is, so the asset is opened here. Loaded on first use, because most strips are never expanded.
    private Bitmap? _before;
    private Bitmap? _after;

    public Bitmap? BeforeImage => _before ??= Load($"{key}-before.png");

    public Bitmap? AfterImage => _after ??= Load($"{key}-after.png");

    // avares addresses resources by assembly name, which is "aculume-gui" here, not the namespace.
    // Taking it from the assembly means renaming the output cannot silently break every image.
    private static readonly string AssemblyName =
        typeof(HelpTopic).Assembly.GetName().Name ?? "aculume-gui";

    private static Bitmap? Load(string file)
    {
        var uri = new Uri($"avares://{AssemblyName}/Assets/help/{file}");
        try
        {
            using var stream = AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (FileNotFoundException)
        {
            // A help image that was never generated must not take the panel down. HelpImagesExist
            // is the guard that keeps this from going unnoticed.
            return null;
        }
    }

    public string Scale => IsExaggerated
        ? "Pushed well past the preset so the effect is visible — at the calibrated value it is much subtler."
        : "Shown at the preset value.";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
