using AcuLume.Gui.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AcuLume.Gui.Views;

public sealed partial class BatchView : UserControl
{
    private static readonly FilePickerFileType ImageFiles = new("Images")
    {
        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.tif", "*.tiff", "*.webp"],
    };

    public BatchView() => InitializeComponent();

    private BatchViewModel? ViewModel => DataContext as BatchViewModel;

    private async void OnAddImages(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || ViewModel is not { } vm)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add images",
            AllowMultiple = true,
            FileTypeFilter = [ImageFiles],
        });

        vm.AddFiles(files.Select(f => f.TryGetLocalPath()).OfType<string>());
    }

    private async void OnAddFolder(object? sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync("Add folder") is { } folder)
        {
            ViewModel?.AddFolder(folder);
        }
    }

    private async void OnChooseFolder(object? sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync("Choose output folder") is { } folder && ViewModel is { } vm)
        {
            vm.OutputFolder = folder;
        }
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private async void OnRun(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            await vm.RunAsync();
        }
    }
}
