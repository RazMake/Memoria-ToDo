using System.Diagnostics;
using Memoria.ToDo.Desktop.FormattedView;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Opens an address in the system's default browser through the Windows shell, as a link opened from any other app.</summary>
internal sealed class ShellDefaultBrowser : IDefaultBrowser
{
    public void Open(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        using var process = Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
    }
}
