using System.ComponentModel;
using AcuLume.Gui.ViewModels;
using Avalonia.Controls;

namespace AcuLume.Gui.Views;

public sealed partial class CompareView : UserControl
{
    /// <summary>Matches the tile margin in the ListBoxItem style.</summary>
    private const double TileGap = 12;

    public CompareView()
    {
        InitializeComponent();

        TileList.SizeChanged += (_, _) => UpdateTileWidth();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is CompareViewModel vm)
            {
                vm.PropertyChanged -= OnViewModelPropertyChanged;
                vm.PropertyChanged += OnViewModelPropertyChanged;
                UpdateTileWidth();
            }
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompareViewModel.Columns))
        {
            UpdateTileWidth();
        }
    }

    private void UpdateTileWidth()
    {
        if (DataContext is not CompareViewModel vm || TileList.Bounds.Width <= 0)
        {
            return;
        }

        var available = TileList.Bounds.Width - (TileGap * vm.Columns);
        vm.TileWidth = Math.Max(120, available / vm.Columns);
    }
}
