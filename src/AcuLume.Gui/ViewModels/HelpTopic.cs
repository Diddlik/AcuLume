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
/// They show synthetic targets rather than photographs, at the preset's real values. On a photograph
/// these settings move a small crop by only a few levels out of 255 — genuinely there, but too
/// little to see — which had forced the earlier photographic pairs to be exaggerated past their
/// settings. A target built for the purpose needs no such dishonesty.
///
/// Settings whose effect is a one or two pixel rim are shown as a plot of brightness across an edge
/// instead of as an image, because that rim is invisible in a tile and unmistakable as a curve.
/// </summary>
public sealed partial class HelpTopic(string key, string explanation, bool isPlot = false) : ObservableObject
{
    public string Explanation { get; } = explanation;

    /// <summary>Whether the pair is a plot across an edge rather than a picture of the target.</summary>
    public bool IsPlot { get; } = isPlot;

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

    public string Scale => IsPlot
        ? "Brightness across an edge, at the preset's own settings. The dashed lines are the original edge, so anything beyond them is overshoot."
        : "A test target at the preset's own settings, magnified twice.";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}
