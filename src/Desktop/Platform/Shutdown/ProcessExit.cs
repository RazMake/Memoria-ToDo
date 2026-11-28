using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Memoria.ToDo.Desktop.Platform;

internal sealed class ProcessExit : IProcessExit
{
    public void RequestExit(int exitCode) => Environment.Exit(exitCode);

    public void Terminate(int exitCode)
    {
        if (!TerminateProcess(GetCurrentProcess(), (uint)exitCode))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(nint process, uint exitCode);
}
