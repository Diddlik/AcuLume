using System.Runtime.InteropServices;
using System.Text;
using NetVips;

namespace AcuLume.Gui.Services;

/// <summary>
/// Colour-manages what the preview puts on screen. Windows does not colour-manage ordinary SDR
/// windows, so on a wide-gamut display even correct sRGB renders oversaturated unless the
/// application converts into the display's own space itself.
///
/// This is display-only. Exported files keep their own pixels and their own profile and must never
/// be converted into a monitor profile.
/// </summary>
public static class DisplayProfile
{
    private static readonly Lazy<string?> ProfilePath = new(ResolvePrimaryMonitorProfile);

    /// <summary>Path to the profile the preview converts into, or null when none could be resolved.</summary>
    public static string? Path => ProfilePath.Value;

    /// <summary>
    /// Converts <paramref name="image"/> into the display's colour space. Untagged images are taken
    /// as sRGB, which is what an untagged file means in practice. Returns the input unchanged when no
    /// profile is available or the transform fails, so a missing lcms or an unreadable profile
    /// degrades to today's behaviour rather than breaking the preview.
    /// </summary>
    public static Image ToDisplay(Image image)
    {
        if (Path is not { } profile)
        {
            return image;
        }

        try
        {
            return image.IccTransform(
                profile,
                inputProfile: "srgb",
                embedded: true,
                intent: Enums.Intent.Relative,
                blackPointCompensation: true);
        }
        catch (VipsException)
        {
            return image;
        }
    }

    private static string? ResolvePrimaryMonitorProfile()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Linux would need the X11 _ICC_PROFILE atom or colord; not implemented (spec section 47
            // makes Linux the second target). Falling through leaves the preview unmanaged there.
            return null;
        }

        var dc = IntPtr.Zero;
        try
        {
            // A null device name gives a DC for the primary display.
            dc = CreateDC("DISPLAY", null, null, IntPtr.Zero);
            if (dc == IntPtr.Zero)
            {
                return null;
            }

            var size = 512u;
            var buffer = new StringBuilder((int)size);
            if (!GetICMProfile(dc, ref size, buffer))
            {
                return null;
            }

            var path = buffer.ToString();
            return File.Exists(path) ? path : null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            if (dc != IntPtr.Zero)
            {
                DeleteDC(dc);
            }
        }
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateDCW")]
    private static extern IntPtr CreateDC(string driver, string? device, string? port, IntPtr mode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetICMProfileW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetICMProfile(IntPtr dc, ref uint size, StringBuilder name);
}
