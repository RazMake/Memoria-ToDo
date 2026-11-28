using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The size in pixels of a notification-area icon, which Windows sizes for the system's scale.</summary>
internal static class NotificationAreaIconSize
{
    private const int SmallIconWidthMetric = 49;  // SM_CXSMICON
    private const int SmallIconHeightMetric = 50; // SM_CYSMICON

    /// <summary>Throws <see cref="Win32Exception"/> when Windows can't give the size.</summary>
    public static System.Drawing.Size Get()
    {
        var dpi = GetDpiForSystem();
        if (dpi == 0)
        {
            throw new Win32Exception("Windows can't give the system's scale.");
        }

        return new System.Drawing.Size(Metric(SmallIconWidthMetric, dpi), Metric(SmallIconHeightMetric, dpi));
    }

    private static int Metric(int index, uint dpi)
    {
        var value = GetSystemMetricsForDpi(index, dpi);
        return value > 0 ? value : throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);
}
