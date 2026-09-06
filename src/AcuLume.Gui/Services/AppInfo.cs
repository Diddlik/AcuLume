using System.Reflection;

namespace AcuLume.Gui.Services;

/// <summary>Single source for the version shown in the sidebar and the settings view.</summary>
public static class AppInfo
{
    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetName().Version?.ToString(2) ?? "0.0";
}
