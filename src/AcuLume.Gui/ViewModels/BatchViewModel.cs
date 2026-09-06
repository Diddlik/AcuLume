using System.Collections.ObjectModel;
using AcuLume.Core;
using AcuLume.Core.Configuration;
using AcuLume.Core.Imaging;
using AcuLume.Gui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AcuLume.Gui.ViewModels;

public enum BatchStatus { Pending, Processing, Done, Failed, Skipped }

public sealed partial class BatchItem(string path) : ObservableObject
{
    public string Path { get; } = path;

    public string FileName { get; } = System.IO.Path.GetFileName(path);

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    [ObservableProperty]
    public partial string Resolution { get; set; } = "…";

    [ObservableProperty]
    public partial BatchStatus Status { get; set; } = BatchStatus.Pending;

    [ObservableProperty]
    public partial string StatusDetail { get; set; } = "Pending";
}

public sealed partial class BatchViewModel : ObservableObject
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp"];

    private readonly PresetLibrary _library;
    private readonly AcuLumeProcessor _processor = new();
    private CancellationTokenSource? _runCts;

    public BatchViewModel(PresetLibrary library)
    {
        _library = library;
        RefreshPresets();
        SelectedPreset = Presets.Contains("web-1800-natural") ? "web-1800-natural" : Presets.FirstOrDefault();
        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SummaryText));
    }

    public ObservableCollection<BatchItem> Items { get; } = [];

    public ObservableCollection<string> Presets { get; } = [];

    [ObservableProperty]
    public partial string? SelectedPreset { get; set; }

    [ObservableProperty]
    public partial string? OutputFolder { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial int Completed { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Add images to build a queue";

    public int SelectedCount => Items.Count(i => i.IsSelected);

    public string SummaryText => $"{Items.Count} images · {SelectedCount} selected";

    public string RunButtonText => IsRunning ? "Cancel" : $"Process {SelectedCount} Images";

    public double Progress => SelectedCount == 0 ? 0 : Completed / (double)SelectedCount;

    public string ProgressText => $"{Completed} / {SelectedCount}";

    public bool CanRun => Items.Count > 0 && OutputFolder is not null && SelectedPreset is not null;

    public IReadOnlyList<InfoRow> OutputSummary => BuildOutputSummary();

    partial void OnSelectedPresetChanged(string? value) => OnPropertyChanged(nameof(OutputSummary));

    partial void OnOutputFolderChanged(string? value) => OnPropertyChanged(nameof(CanRun));

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(RunButtonText));

    partial void OnCompletedChanged(int value)
    {
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ProgressText));
    }

    public void RefreshPresets()
    {
        var previous = SelectedPreset;
        Presets.Clear();
        foreach (var entry in _library.Load())
        {
            Presets.Add(entry.Name);
        }

        if (previous is not null && Presets.Contains(previous))
        {
            SelectedPreset = previous;
        }
    }

    public void AddFiles(IEnumerable<string> paths)
    {
        var existing = Items.Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (!existing.Add(path) || !SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var item = new BatchItem(path);
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BatchItem.IsSelected))
                {
                    NotifySelectionChanged();
                }
            };
            Items.Add(item);
            ReadResolution(item);
        }

        NotifySelectionChanged();
        StatusText = $"{Items.Count} images queued";
    }

    public void AddFolder(string folder) => AddFiles(
        Directory.EnumerateFiles(folder).Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)));

    [RelayCommand]
    private void RemoveSelected()
    {
        foreach (var item in Items.Where(i => i.IsSelected).ToList())
        {
            Items.Remove(item);
        }

        NotifySelectionChanged();
    }

    [RelayCommand]
    private void Clear()
    {
        Items.Clear();
        Completed = 0;
        NotifySelectionChanged();
    }

    public async Task RunAsync()
    {
        if (IsRunning)
        {
            await (_runCts?.CancelAsync() ?? Task.CompletedTask);
            return;
        }

        if (!CanRun || OutputFolder is not { } folder || SelectedPreset is not { } presetName)
        {
            return;
        }

        if (_library.Load().FirstOrDefault(e => e.Name == presetName) is not { } entry)
        {
            StatusText = $"Preset '{presetName}' is no longer available";
            return;
        }

        var options = PresetLoader.ToProcessingOptions(entry.Preset);
        var queue = Items.Where(i => i.IsSelected).ToList();

        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        IsRunning = true;
        Completed = 0;

        foreach (var item in queue)
        {
            item.Status = BatchStatus.Pending;
            item.StatusDetail = "Pending";
        }

        try
        {
            foreach (var item in queue)
            {
                token.ThrowIfCancellationRequested();
                await ProcessAsync(item, folder, options, token).ConfigureAwait(true);
                Completed++;
            }

            StatusText = $"Finished {Completed} of {queue.Count} images";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Cancelled after {Completed} of {queue.Count} images";
            foreach (var item in queue.Where(i => i.Status == BatchStatus.Pending))
            {
                item.Status = BatchStatus.Skipped;
                item.StatusDetail = "Skipped";
            }
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task ProcessAsync(BatchItem item, string folder, ProcessingOptions options, CancellationToken token)
    {
        item.Status = BatchStatus.Processing;
        item.StatusDetail = "Processing";

        // Never write next to the source, and never over it: the output folder plus a marked name.
        var outputPath = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(item.Path)}.aculume.jpg");

        try
        {
            var result = await Task.Run(
                () => _processor.Process(item.Path, outputPath, options, token), token).ConfigureAwait(true);
            item.Status = BatchStatus.Done;
            item.StatusDetail = $"{result.OutputWidth} × {result.OutputHeight} · {result.Elapsed.TotalSeconds:F1} s";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AcuLumeProcessingException ex)
        {
            item.Status = BatchStatus.Failed;
            item.StatusDetail = $"{ex.Stage}: {ex.Message}";
        }
        catch (Exception ex)
        {
            item.Status = BatchStatus.Failed;
            item.StatusDetail = ex.Message;
        }
    }

    private void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(RunButtonText));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(CanRun));
    }

    private static void ReadResolution(BatchItem item)
    {
        try
        {
            var metadata = ImageLoader.ReadMetadata(item.Path);
            item.Resolution = $"{metadata.OrientedWidth} × {metadata.OrientedHeight}";
        }
        catch (Exception ex)
        {
            item.Resolution = "unreadable";
            item.Status = BatchStatus.Failed;
            item.StatusDetail = ex.Message;
        }
    }

    private IReadOnlyList<InfoRow> BuildOutputSummary()
    {
        if (SelectedPreset is not { } name
            || _library.Load().FirstOrDefault(e => e.Name == name) is not { } entry)
        {
            return [];
        }

        var preset = entry.Preset;
        return
        [
            new InfoRow("Resize", preset.Resize is { Enabled: true, LongEdge: { } edge }
                ? $"Long edge {edge} px"
                : "Full resolution"),
            new InfoRow("Format", "JPEG"),
            new InfoRow("Quality", $"{preset.Output?.Quality ?? 90}"),
            new InfoRow("Metadata", "Preserved"),
            new InfoRow("Naming", "<name>.aculume.jpg"),
        ];
    }
}
