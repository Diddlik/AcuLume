using AcuLume.Gui.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AcuLume.Gui.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Tunnelling, so a shortcut works wherever focus sits — except in a text field, where the
        // plain digit and letter keys belong to the editor.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { IsSharpenActive: true }
            || FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        e.Handled = Sharpen.HandleShortcut(e);
    }
}
