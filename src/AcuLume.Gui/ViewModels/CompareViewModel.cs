using System.Collections.ObjectModel;
using AcuLume.Core.Comparison;
using AcuLume.Gui.Services;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcuLume.Gui.ViewModels;

public sealed partial class CompareTile(string label, string description, Bitmap image, string? presetName)
    : ObservableObject
{
    public string Label { get; } = label;

    public string Description { get; } = description;

    public Bitmap Image { get; } = image;

    /// <summary>Non-null when the variant corresponds to a preset that can be loaded into Sharpen.</summary>
    public string? PresetName { get; } = presetName;

    /// <summary>
    /// Selection lives on the tile: the grid is a plain ItemsControl, because a ListBox's own
    /// ScrollViewer nested in the page's ScrollViewer sends layout into unbounded recursion.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Side-by-side view of the spec's comparison set (section 43): original, resize-only, a naive
/// unsharp-mask baseline and the built-in AcuLume presets — so the pipeline's benefit can be judged
/// rather than assumed. Built by the same <see cref="ComparisonSetBuilder"/> the CLI uses.
/// </summary>
public sealed partial class CompareViewModel(SharpenViewModel sharpen, PresetLibrary library) : ObservableObject
{
    private static readonly Dictionary<string, (string Label, string Description, string? Preset)> Descriptions = new()
    {
        ["original"] = ("ORIGINAL", "Untouched source file.", null),
        ["resize-only"] = ("RESIZE ONLY", "Lanczos downscale, no sharpening.", null),
        ["usm-baseline"] = ("USM BASELINE", "Naive unsharp mask, the thing to beat.", null),
        ["aculume-natural"] = ("ACULUME NATURAL", "Balanced multi-band output sharpening.", "web-1800-natural"),
        ["aculume-crisp"] = ("ACULUME CRISP", "Stronger variant for detailed subjects.", "web-1800-crisp"),
    };

    private string? _builtFor;

    public ObservableCollection<CompareTile> Tiles { get; } = [];

    [ObservableProperty]
    public partial int Columns { get; set; } = 3;

    /// <summary>Tile width in pixels; the view sets it from its own width and <see cref="Columns"/>.</summary>
    [ObservableProperty]
    public partial double TileWidth { get; set; } = 320;

    [ObservableProperty]
    public partial CompareTile? Selected { get; set; }

    [ObservableProperty]
    public partial bool IsBuilding { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Open an image in Sharpen, then build a comparison set";

    public bool HasImage => sharpen.ImagePath is not null;

    public string ApplyButtonText => Selected?.PresetName is { } name ? $"Use {name}" : "Use selected variant";

    [RelayCommand]
    private void Select(CompareTile tile) => Selected = tile;

    partial void OnSelectedChanged(CompareTile? value)
    {
        foreach (var tile in Tiles)
        {
            tile.IsSelected = ReferenceEquals(tile, value);
        }

        OnPropertyChanged(nameof(ApplyButtonText));
        OnPropertyChanged(nameof(CanApply));
    }

    public bool CanApply => Selected?.PresetName is not null;

    /// <summary>Rebuilds only when the image changed, since a full set is five encode passes.</summary>
    public async Task EnsureBuiltAsync()
    {
        OnPropertyChanged(nameof(HasImage));

        if (sharpen.ImagePath is not { } path)
        {
            Tiles.Clear();
            StatusText = "Open an image in Sharpen, then build a comparison set";
            return;
        }

        if (_builtFor == path && Tiles.Count > 0)
        {
            return;
        }

        await BuildAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task BuildAsync()
    {
        if (sharpen.ImagePath is not { } path || IsBuilding)
        {
            return;
        }

        IsBuilding = true;
        StatusText = "Building comparison set…";

        try
        {
            var options = new ComparisonSetOptions { LongEdge = sharpen.LongEdge, Quality = sharpen.Quality };
            var directory = Path.Combine(Path.GetTempPath(), "AcuLume", "compare");

            var variants = await Task.Run(
                () => ComparisonSetBuilder.Build(path, directory, options)).ConfigureAwait(true);

            Tiles.Clear();
            foreach (var variant in variants)
            {
                var (label, description, preset) = Descriptions.TryGetValue(variant.Name, out var d)
                    ? d
                    : (variant.Name.ToUpperInvariant(), string.Empty, null);
                Tiles.Add(new CompareTile(label, description, PreviewRenderer.LoadForDisplay(variant.Path), preset));
            }

            Selected = Tiles.FirstOrDefault(t => t.PresetName is not null);
            _builtFor = path;
            StatusText = $"{Tiles.Count} variants at {sharpen.LongEdge} px long edge";
        }
        catch (Exception ex)
        {
            StatusText = $"Comparison failed: {ex.Message}";
        }
        finally
        {
            IsBuilding = false;
        }
    }

    [RelayCommand]
    private void ApplySelected()
    {
        if (Selected?.PresetName is not { } name
            || library.Load().FirstOrDefault(e => e.Name == name) is not { } entry)
        {
            return;
        }

        sharpen.SelectedPreset = name;
        sharpen.Apply(entry.Preset);
        StatusText = $"Loaded '{name}' into the Sharpen panel";
    }
}
