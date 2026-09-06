using AcuLume.Gui.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AcuLume.Gui.Views;

public sealed partial class PresetsView : UserControl
{
    private static readonly FilePickerFileType PresetFiles = new("AcuLume preset") { Patterns = ["*.json"] };

    public PresetsView() => InitializeComponent();

    private PresetsViewModel? ViewModel => DataContext as PresetsViewModel;

    private async void OnImport(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || ViewModel is not { } vm)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import preset",
            AllowMultiple = false,
            FileTypeFilter = [PresetFiles],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            vm.Import(path);
        }
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || ViewModel?.Selected is not { } card)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export preset",
            SuggestedFileName = $"{card.Name}.json",
            DefaultExtension = "json",
            FileTypeChoices = [PresetFiles],
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            ViewModel.Export(path);
        }
    }

    private async void OnRename(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { Selected: { IsBuiltIn: false } card } vm
            || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        if (await TextPromptWindow.ShowAsync(owner, "Rename preset", card.Name) is { } name)
        {
            vm.Rename(name);
        }
    }
}
