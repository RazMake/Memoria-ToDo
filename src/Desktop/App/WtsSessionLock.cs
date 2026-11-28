using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.KeepAwake;
using Microsoft.Win32;

namespace Memoria.ToDo.Desktop.App;

/// <summary>Reads from Windows each time whether this session is locked or disconnected, and raises <see cref="Changed"/> on the thread that made it.</summary>
internal sealed class WtsSessionLock : ISessionLock
{
    private const uint CurrentSession = unchecked((uint)-1); // WTS_CURRENT_SESSION
    private const int SessionInfoEx = 25;                    // WTSSessionInfoEx
    private const int Level1 = 1;
    private const int Disconnected = 4;                      // WTSDisconnected
    private const int LockedFlag = 0;                        // WTS_SESSIONSTATE_LOCK
    private const int UnlockedFlag = 1;                      // WTS_SESSIONSTATE_UNLOCK

    // WTSINFOEXW: Level (4 bytes), then the 8-aligned level 1 data: SessionId, SessionState, SessionFlags.
    private const int StateOffset = 12;
    private const int FlagsOffset = 16;

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private EventHandler? _changed;

    public event EventHandler? Changed
    {
        add
        {
            if (_changed is null)
            {
                SystemEvents.SessionSwitch += OnSessionSwitch;
            }

            _changed += value;
        }
        remove
        {
            _changed -= value;
            if (_changed is null)
            {
                SystemEvents.SessionSwitch -= OnSessionSwitch;
            }
        }
    }

    /// <summary>Throws <see cref="Win32Exception"/> when Windows can't say.</summary>
    public bool IsLocked
    {
        get
        {
            if (!WTSQuerySessionInformation(IntPtr.Zero, CurrentSession, SessionInfoEx, out var buffer, out _))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            try
            {
                if (Marshal.ReadInt32(buffer) != Level1)
                {
                    throw new Win32Exception("Windows gave the session's state in an unknown form.");
                }

                var flags = Marshal.ReadInt32(buffer, FlagsOffset);
                return flags switch
                {
                    LockedFlag => true,
                    UnlockedFlag => Marshal.ReadInt32(buffer, StateOffset) == Disconnected,
                    _ => throw new Win32Exception("Windows can't say whether the session is locked."),
                };
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionUnlock
            or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.ConsoleDisconnect
            or SessionSwitchReason.RemoteConnect or SessionSwitchReason.RemoteDisconnect)
        {
            _dispatcher.BeginInvoke(() => _changed?.Invoke(this, EventArgs.Empty));
        }
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr server, uint sessionId, int infoClass, out IntPtr buffer, out uint bytes);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);
}
