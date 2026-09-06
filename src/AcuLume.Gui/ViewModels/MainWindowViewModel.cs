using AcuLume.Gui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcuLume.Gui.ViewModels;

public enum Section { Sharpen, Batch, Presets, Compare, ImageInfo, Settings }

public sealed partial class NavItem(Section section, string label, string icon) : ObservableObject
{
    public Section Section { get; } = section;

    public string Label { get; } = label;

    /// <summary>SVG path geometry for the item's icon, transcribed from the design mockup.</summary>
    public string Icon { get; } = icon;

    [ObservableProperty]
    public partial bool IsActive { get; set; }
}

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly PresetLibrary _library = new();

    public MainWindowViewModel()
    {
        Sharpen = new SharpenViewModel(_library, new RecentImages());
        Presets = new PresetsViewModel(_library, Sharpen);
        Batch = new BatchViewModel(_library);
        Compare = new CompareViewModel(Sharpen, _library);
        ImageInfo = new ImageInfoViewModel(Sharpen);
        Settings = new SettingsViewModel(_library);

        SyncNavigation();
    }

    public IReadOnlyList<NavItem> NavItems { get; } =
    [
        new(Section.Sharpen, "Sharpen", "M4 18l5-12 4 9 3-5 4 8"),
        new(Section.Batch, "Batch", "M4 7h16M4 12h16M4 17h16"),
        new(Section.Presets, "Presets", "M7 4h10v16l-5-4-5 4z"),
        new(Section.Compare, "Compare", "M12 3v18M6 7l-3 5 3 5M18 7l3 5-3 5"),
        new(Section.ImageInfo, "Image Info", "M12 21a9 9 0 100-18 9 9 0 000 18M12 8v.01M12 11v6"),
        new(Section.Settings, "Settings", "M12 9.5a2.5 2.5 0 100 5 2.5 2.5 0 000-5M4 12h1.6M18.4 12H20M12 4v1.6M12 18.4V20M6.8 6.8l1.1 1.1M16.1 16.1l1.1 1.1M17.2 6.8l-1.1 1.1M7.9 16.1l-1.1 1.1"),
    ];

    [ObservableProperty]
    public partial Section ActiveSection { get; set; } = Section.Sharpen;

    public SharpenViewModel Sharpen { get; }

    public PresetsViewModel Presets { get; }

    public BatchViewModel Batch { get; }

    public CompareViewModel Compare { get; }

    public ImageInfoViewModel ImageInfo { get; }

    public SettingsViewModel Settings { get; }

    public bool IsSharpenActive => ActiveSection == Section.Sharpen;

    public bool IsBatchActive => ActiveSection == Section.Batch;

    public bool IsPresetsActive => ActiveSection == Section.Presets;

    public bool IsCompareActive => ActiveSection == Section.Compare;

    public bool IsImageInfoActive => ActiveSection == Section.ImageInfo;

    public bool IsSettingsActive => ActiveSection == Section.Settings;

    public string VersionLine => $"AcuLume {AppInfo.Version} · Local processing";

    [RelayCommand]
    private void Navigate(Section section) => ActiveSection = section;

    [RelayCommand]
    private async Task OpenRecent(RecentImage entry)
    {
        ActiveSection = Section.Sharpen;
        await Sharpen.LoadImageAsync(entry.Path);
    }

    partial void OnActiveSectionChanged(Section value)
    {
        foreach (var name in new[]
                 {
                     nameof(IsSharpenActive), nameof(IsBatchActive), nameof(IsPresetsActive),
                     nameof(IsCompareActive), nameof(IsImageInfoActive), nameof(IsSettingsActive),
                 })
        {
            OnPropertyChanged(name);
        }

        SyncNavigation();

        // These views are snapshots of state owned elsewhere, so they refresh on entry rather than
        // subscribing to every change while hidden.
        switch (value)
        {
            case Section.ImageInfo:
                ImageInfo.Refresh();
                break;
            case Section.Presets:
                Presets.Refresh();
                break;
            case Section.Batch:
                Batch.RefreshPresets();
                break;
            case Section.Compare:
                _ = Compare.EnsureBuiltAsync();
                break;
        }
    }

    private void SyncNavigation()
    {
        foreach (var item in NavItems)
        {
            item.IsActive = item.Section == ActiveSection;
        }
    }

    public void Dispose() => Sharpen.Dispose();
}
