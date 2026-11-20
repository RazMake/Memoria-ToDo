using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Hides a window from the screen, or shows it again, without changing its state (DWMWA_CLOAK).</summary>
internal static class WindowCloak
{
    private const int Cloak = 13;

    /// <exception cref="Win32Exception">Windows can't cloak or uncloak the window.</exception>
    public static void Set(nint handle, bool cloaked)
    {
        var value = cloaked ? 1 : 0;
        var result = DwmSetWindowAttribute(handle, Cloak, ref value, sizeof(int));
        if (result != 0)
        {
            throw new Win32Exception(result);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
