using AcuLume.Gui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcuLume.Gui.ViewModels;

/// <summary>
/// Facts about how this build processes images, and the shortcuts the Sharpen view actually binds.
/// Everything here is read from the running engine rather than restated by hand, so it cannot drift.
/// The update section is the only part that acts rather than reports.
/// </summary>
public sealed partial class SettingsViewModel(PresetLibrary library, UpdateService updates) : ObservableObject
{
    public IReadOnlyList<InfoRow> Processing { get; } =
    [
        new("Engine", $"AcuLume {AppInfo.Version}"),
        new("Image library", $"libvips {NetVips.NetVips.Version(0)}.{NetVips.NetVips.Version(1)}.{NetVips.NetVips.Version(2)}"),
        new("Resize kernel", "Lanczos 3"),
        new("Sharpening space", "Lab, L channel only"),
        new("Precision", "32-bit float"),
        new("Determinism", "Same input + preset ⇒ same output"),
        new("Network access", "Only when you press Check for updates"),
        new("Preview display profile", DisplayProfile.Path is { } profile
            ? Path.GetFileName(profile)
            : "None — preview shown unmanaged"),
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

    public string InstalledVersion => $"AcuLume {updates.CurrentVersion}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(CanCheck))]
    [NotifyPropertyChangedFor(nameof(CanDownload))]
    [NotifyPropertyChangedFor(nameof(CanRestart))]
    [NotifyPropertyChangedFor(nameof(ShowProgress))]
    public partial UpdateState State { get; set; } = UpdateState.Idle;

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } =
        "Checked only when you ask, and downloaded only after that.";

    [ObservableProperty]
    public partial int DownloadProgress { get; set; }

    public bool IsBusy => State is UpdateState.Checking or UpdateState.Downloading;

    public bool CanCheck => !IsBusy;

    public bool CanDownload => State == UpdateState.UpdateAvailable;

    public bool CanRestart => State == UpdateState.ReadyToRestart;

    public bool ShowProgress => State == UpdateState.Downloading;

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        State = UpdateState.Checking;
        UpdateStatus = "Asking GitHub for the latest release…";
        try
        {
            State = await updates.CheckAsync().ConfigureAwait(true);
            UpdateStatus = State switch
            {
                UpdateState.NotInstalled =>
                    "This copy was not installed, so it cannot update itself. Install a release to enable updates.",
                UpdateState.UpToDate => $"AcuLume {updates.CurrentVersion} is the latest release.",
                UpdateState.UpdateAvailable => $"Version {updates.AvailableVersion} is available.",
                _ => UpdateStatus,
            };
        }
        catch (Exception ex)
        {
            State = UpdateState.Failed;
            UpdateStatus = $"Could not check for updates: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        var version = updates.AvailableVersion;
        State = UpdateState.Downloading;
        DownloadProgress = 0;
        UpdateStatus = $"Downloading {version}…";
        try
        {
            var progress = new Progress<int>(p => DownloadProgress = p);
            State = await updates.DownloadAsync(progress).ConfigureAwait(true);
            UpdateStatus = State == UpdateState.ReadyToRestart
                ? $"Version {version} is ready — it is applied when AcuLume restarts."
                : "The download did not complete.";
        }
        catch (Exception ex)
        {
            State = UpdateState.Failed;
            UpdateStatus = $"Download failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RestartAndUpdate() => updates.ApplyAndRestart();
}
