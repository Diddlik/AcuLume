using System.Collections.ObjectModel;
using AcuLume.Core.Configuration;
using AcuLume.Gui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcuLume.Gui.ViewModels;

/// <summary>One preset card in the library grid.</summary>
public sealed partial class PresetCard(PresetEntry entry) : ObservableObject
{
    public PresetEntry Entry { get; } = entry;

    /// <summary>
    /// Selection lives on the card rather than on a list control: the cards render in a plain
    /// ItemsControl, because a ListBox brings its own ScrollViewer and nesting that inside the
    /// page's ScrollViewer sends layout into unbounded recursion.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string Name => Entry.Name;

    public bool IsBuiltIn => Entry.IsBuiltIn;

    public string Tag => Entry.Preset.Experimental ? "EXPERIMENTAL" : Entry.IsBuiltIn ? "BUILT-IN" : "USER";

    public string Description => Entry.Preset.Description ?? "No description.";

    public string FineSummary => Format(Entry.Preset.OutputSharpen?.Fine);

    public string MediumSummary => Format(Entry.Preset.OutputSharpen?.Medium);

    public string DarkLightSummary => Entry.Preset.OutputSharpen?.Fine is { } fine
        ? $"D/L {fine.DarkAmount:0.00}/{fine.LightAmount:0.00}"
        : "D/L —";

    public string SizeSummary => Entry.Preset.Resize is { Enabled: true, LongEdge: { } edge }
        ? $"{edge} px"
        : "full size";

    private static string Format(BandPresetOptions? band) =>
        band is null ? "—" : $"{band.Amount:0.00} @ {band.Radius:0.00} px";
}

public sealed partial class PresetsViewModel : ObservableObject
{
    private readonly PresetLibrary _library;
    private readonly SharpenViewModel _sharpen;

    public PresetsViewModel(PresetLibrary library, SharpenViewModel sharpen)
    {
        _library = library;
        _sharpen = sharpen;
        Refresh();
    }

    public ObservableCollection<PresetCard> BuiltIn { get; } = [];

    public ObservableCollection<PresetCard> User { get; } = [];

    [ObservableProperty]
    public partial PresetCard? Selected { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public ObservableCollection<InfoRow> SelectedParameters { get; } = [];

    public string UserPresetDirectory => _library.UserPresetDirectory;

    [RelayCommand]
    private void Select(PresetCard card) => Selected = card;

    partial void OnSelectedChanged(PresetCard? value)
    {
        foreach (var card in BuiltIn.Concat(User))
        {
            card.IsSelected = ReferenceEquals(card, value);
        }

        SelectedParameters.Clear();
        OnPropertyChanged(nameof(CanEditSelected));

        if (value?.Entry.Preset.OutputSharpen is not { } sharpen)
        {
            return;
        }

        if (sharpen.Fine is { } fine)
        {
            SelectedParameters.Add(new InfoRow("Fine amount", $"{fine.Amount:0.00}"));
            SelectedParameters.Add(new InfoRow("Fine radius", $"{fine.Radius:0.00} px"));
        }

        if (sharpen.Medium is { } medium)
        {
            SelectedParameters.Add(new InfoRow("Medium amount", $"{medium.Amount:0.00}"));
            SelectedParameters.Add(new InfoRow("Medium radius", $"{medium.Radius:0.00} px"));
        }

        SelectedParameters.Add(new InfoRow("Noise protection", $"{sharpen.NoiseProtection:0.00}"));
        SelectedParameters.Add(new InfoRow("Edge protection", $"{sharpen.EdgeProtection:0.00}"));
        SelectedParameters.Add(new InfoRow("Halo limiter", $"{sharpen.HaloProtection:0.00}"));
        SelectedParameters.Add(new InfoRow("Output", value.SizeSummary));
        SelectedParameters.Add(new InfoRow("Quality", $"{value.Entry.Preset.Output?.Quality ?? 90}"));
    }

    public bool CanEditSelected => Selected is { IsBuiltIn: false };

    public void Refresh()
    {
        var previous = Selected?.Name;

        BuiltIn.Clear();
        User.Clear();
        foreach (var entry in _library.Load())
        {
            (entry.IsBuiltIn ? BuiltIn : User).Add(new PresetCard(entry));
        }

        Selected = BuiltIn.Concat(User).FirstOrDefault(c => c.Name == previous)
            ?? BuiltIn.Concat(User).FirstOrDefault();

        _sharpen.RefreshPresets();
    }

    [RelayCommand]
    private void Duplicate()
    {
        if (Selected is not { } card)
        {
            return;
        }

        var name = _library.UniqueName($"{card.Name} copy");
        _library.Save(card.Entry.Preset with { Name = name });
        Refresh();
        Selected = User.FirstOrDefault(c => c.Name == name) ?? Selected;
        StatusText = $"Created '{name}'";
    }

    [RelayCommand]
    private void SaveCurrentSettings()
    {
        var name = _library.UniqueName("My preset");
        _library.Save(_sharpen.ToPreset(name, "Saved from the Sharpen panel."));
        Refresh();
        Selected = User.FirstOrDefault(c => c.Name == name) ?? Selected;
        StatusText = $"Saved current Sharpen settings as '{name}'";
    }

    public void Rename(string newName)
    {
        if (Selected is not { IsBuiltIn: false } card || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        var name = _library.UniqueName(newName.Trim());
        PresetLibrary.Delete(card.Entry);
        _library.Save(card.Entry.Preset with { Name = name });
        Refresh();
        Selected = User.FirstOrDefault(c => c.Name == name) ?? Selected;
        StatusText = $"Renamed to '{name}'";
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is not { IsBuiltIn: false } card)
        {
            return;
        }

        PresetLibrary.Delete(card.Entry);
        StatusText = $"Deleted '{card.Name}'";
        Selected = null;
        Refresh();
    }

    public void Import(string path)
    {
        try
        {
            _library.Import(path);
            Refresh();
            StatusText = $"Imported {Path.GetFileName(path)}";
        }
        catch (PresetValidationException ex)
        {
            StatusText = $"Import failed: {ex.Message}";
        }
    }

    public void Export(string path)
    {
        if (Selected is { } card)
        {
            PresetLibrary.Export(card.Entry, path);
            StatusText = $"Exported to {Path.GetFileName(path)}";
        }
    }

    [RelayCommand]
    private void ApplyToCurrentImage()
    {
        if (Selected is { } card)
        {
            _sharpen.SelectedPreset = card.Name;
            _sharpen.Apply(card.Entry.Preset);
            StatusText = $"Applied '{card.Name}' to the Sharpen panel";
        }
    }
}
