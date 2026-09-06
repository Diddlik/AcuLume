using System.ComponentModel;
using AcuLume.Gui.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace AcuLume.Gui.Views;

public sealed partial class SharpenView : UserControl
{
    private static readonly FilePickerFileType ImageFiles = new("Images")
    {
        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.tif", "*.tiff", "*.webp"],
    };

    private bool _draggingSplit;

    public SharpenView()
    {
        InitializeComponent();

        PreviewViewport.SizeChanged += (_, _) => UpdatePreviewSize();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private SharpenViewModel? ViewModel => DataContext as SharpenViewModel;

    private void AttachViewModel()
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdatePreviewSize();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SharpenViewModel.PreviewPixelWidth)
            or nameof(SharpenViewModel.PreviewPixelHeight)
            or nameof(SharpenViewModel.OutputWidth)
            or nameof(SharpenViewModel.OutputHeight))
        {
            UpdatePreviewSize();
        }
    }

    /// <summary>
    /// Sizes the preview host to exactly the displayed image.
    ///
    /// "Fit" has to be computed explicitly for two reasons: inside a ScrollViewer the content is
    /// measured with infinite width, so an auto-sized host would take the image's natural size; and
    /// the host must keep the image's aspect ratio, because the overlays — split divider, corner
    /// labels, histogram — position against the host and would drift into the letterbox otherwise.
    /// </summary>
    private void UpdatePreviewSize()
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        if (vm.Zoom != ZoomMode.Fit)
        {
            PreviewHost.Width = vm.PreviewPixelWidth;
            PreviewHost.Height = vm.PreviewPixelHeight;
            return;
        }

        var padding = PreviewViewport.Padding;
        var available = new Size(
            Math.Max(1, PreviewViewport.Bounds.Width - padding.Left - padding.Right),
            Math.Max(1, PreviewViewport.Bounds.Height - padding.Top - padding.Bottom));

        if (vm.OutputWidth <= 0 || vm.OutputHeight <= 0)
        {
            PreviewHost.Width = available.Width;
            PreviewHost.Height = available.Height;
            return;
        }

        var scale = Math.Min(
            available.Width / vm.OutputWidth,
            available.Height / vm.OutputHeight);

        PreviewHost.Width = Math.Max(1, vm.OutputWidth * scale);
        PreviewHost.Height = Math.Max(1, vm.OutputHeight * scale);
    }

    private async void OnOpenImage(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await OpenImageAsync();

    private async void OnExport(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExportAsync();

    public async Task OpenImageAsync()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || ViewModel is not { } vm)
        {
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open image",
            AllowMultiple = false,
            FileTypeFilter = [ImageFiles],
        });

        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        await vm.LoadImageAsync(path);
        UpdatePreviewSize();
    }

    public async Task ExportAsync()
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel || ViewModel is not { } vm || !vm.HasImage)
        {
            return;
        }

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export sharpened image",
            SuggestedFileName = vm.SuggestedOutputName,
            DefaultExtension = "jpg",
            FileTypeChoices = [ImageFiles],
        });

        if (file?.TryGetLocalPath() is { } path)
        {
            await vm.ExportAsync(path);
        }
    }

    private async void OnSavePreset(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not { } vm || TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        if (await TextPromptWindow.ShowAsync(owner, "Save preset as", vm.SuggestedPresetName) is { } name)
        {
            vm.SaveAsPreset(name);
        }
    }

    /// <summary>Handles the shortcuts listed in Settings. Returns false when the key isn't one of them.</summary>
    public bool HandleShortcut(KeyEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return false;
        }

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.O:
                    _ = OpenImageAsync();
                    return true;
                case Key.E:
                    _ = ExportAsync();
                    return true;
                default:
                    return false;
            }
        }

        if (e.KeyModifiers != KeyModifiers.None)
        {
            return false;
        }

        switch (e.Key)
        {
            case Key.Space:
                vm.PreviewMode = vm.PreviewMode == PreviewMode.After ? PreviewMode.Before : PreviewMode.After;
                return true;
            case Key.B:
                vm.PreviewMode = vm.PreviewMode == PreviewMode.Split ? PreviewMode.After : PreviewMode.Split;
                return true;
            case Key.D1:
                vm.Zoom = ZoomMode.Fit;
                return true;
            case Key.D2:
                vm.Zoom = ZoomMode.Percent100;
                return true;
            case Key.D3:
                vm.Zoom = ZoomMode.Percent200;
                return true;
            default:
                return false;
        }
    }

    private void OnSplitPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _draggingSplit = true;
        e.Pointer.Capture((IInputElement)sender!);
        MoveSplit(e.GetPosition(PreviewHost).X);
    }

    private void OnSplitPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingSplit)
        {
            MoveSplit(e.GetPosition(PreviewHost).X);
        }
    }

    private void OnSplitPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _draggingSplit = false;
        e.Pointer.Capture(null);
    }

    private void MoveSplit(double x)
    {
        var width = PreviewHost.Bounds.Width;
        if (ViewModel is { } vm && width > 0)
        {
            vm.SplitPosition = Math.Clamp(x / width, 0, 1);
        }
    }
}
