using AcuLume.Gui.ViewModels;
using AcuLume.Gui.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AcuLume.Gui;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };
            desktop.ShutdownRequested += (_, _) => viewModel.Dispose();

            // Supports "Open with" / drag-onto-executable: aculume-gui <image>
            if (desktop.Args is [var path, ..] && File.Exists(path))
            {
                _ = viewModel.Sharpen.LoadImageAsync(path);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
