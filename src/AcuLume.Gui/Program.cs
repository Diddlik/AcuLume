using Avalonia;
using Velopack;

namespace AcuLume.Gui;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run before anything else: on an install, update or uninstall the app is launched with
        // hook arguments, and this call performs that work and exits instead of showing a window.
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Referenced by the Avalonia XAML previewer/designer, which requires this exact signature.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
