using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AcuLume.Gui.Views;

/// <summary>Minimal modal text prompt; returns null when cancelled.</summary>
public sealed partial class TextPromptWindow : Window
{
    public TextPromptWindow() => InitializeComponent();

    public static async Task<string?> ShowAsync(Window owner, string title, string initialValue)
    {
        var window = new TextPromptWindow { Title = title };
        window.Input.Text = initialValue;
        window.Input.SelectAll();

        return await window.ShowDialog<string?>(owner);
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) =>
        Close(string.IsNullOrWhiteSpace(Input.Text) ? null : Input.Text);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                OnConfirm(sender, e);
                break;
            case Key.Escape:
                OnCancel(sender, e);
                break;
        }
    }
}
