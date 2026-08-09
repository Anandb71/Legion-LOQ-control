using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LegionLoqControl.Presentation;

internal static class WindowTheme
{
    private const int UseImmersiveDarkModeBefore20H1 = 19;
    private const int UseImmersiveDarkMode = 20;

    public static void TryEnableDarkTitleBar(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
            return;

        int enabled = 1;
        int result = DwmSetWindowAttribute(
            handle,
            UseImmersiveDarkMode,
            in enabled,
            sizeof(int));
        if (result < 0)
        {
            _ = DwmSetWindowAttribute(
                handle,
                UseImmersiveDarkModeBefore20H1,
                in enabled,
                sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        in int value,
        int valueSize);
}
