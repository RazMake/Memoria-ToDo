using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// Windows' own system-wide hot keys (RegisterHotKey). The registrations belong to a message-only window made on the
/// first registration, on the calling UI thread, and closed with the last one, so a press runs on that thread.
/// </summary>
internal sealed class HotKeyShortcut : IGlobalShortcut
{
    private const int WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotKeyAlreadyRegistered = 1409;
    private static readonly nint HwndMessage = -3; // HWND_MESSAGE: a window that only receives messages.

    private readonly Dictionary<Shortcut, Registration> _registered = [];
    private HwndSource? _window;
    private int _lastId;

    public bool Register(Shortcut shortcut, Action pressed)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(pressed);
        if (_registered.ContainsKey(shortcut))
        {
            return false;
        }

        var window = _window ??= CreateWindow();
        var id = ++_lastId;
        if (!RegisterHotKey(window.Handle, id, ModifiersOf(shortcut) | ModNoRepeat, (uint)KeyInterop.VirtualKeyFromKey(shortcut.Key)))
        {
            var error = Marshal.GetLastWin32Error();
            CloseWindowWhenUnused();
            return error == ErrorHotKeyAlreadyRegistered ? false : throw new Win32Exception(error);
        }

        _registered.Add(shortcut, new Registration(id, pressed));
        return true;
    }

    public void Unregister(Shortcut shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        if (!_registered.TryGetValue(shortcut, out var registration))
        {
            return;
        }

        // Forget the shortcut only once Windows has released it, so a failed release leaves its press working.
        if (!UnregisterHotKey(_window!.Handle, registration.Id))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        _registered.Remove(shortcut);
        CloseWindowWhenUnused();
    }

    private HwndSource CreateWindow()
    {
        var window = new HwndSource(new HwndSourceParameters("Memoria-ToDo hot keys") { ParentWindow = HwndMessage });
        window.AddHook(OnMessage);
        return window;
    }

    private void CloseWindowWhenUnused()
    {
        if (_registered.Count == 0)
        {
            _window?.Dispose();
            _window = null;
        }
    }

    private nint OnMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotKey)
        {
            foreach (var registration in _registered.Values)
            {
                if (registration.Id == (int)wParam)
                {
                    handled = true;
                    registration.Pressed();
                    break;
                }
            }
        }

        return 0;
    }

    private static uint ModifiersOf(Shortcut shortcut)
    {
        var modifiers = shortcut.Modifiers;
        return (modifiers.HasFlag(ModifierKeys.Alt) ? 0x1u : 0)      // MOD_ALT
            | (modifiers.HasFlag(ModifierKeys.Control) ? 0x2u : 0)   // MOD_CONTROL
            | (modifiers.HasFlag(ModifierKeys.Shift) ? 0x4u : 0)     // MOD_SHIFT
            | (modifiers.HasFlag(ModifierKeys.Windows) ? 0x8u : 0);  // MOD_WIN
    }

    private readonly record struct Registration(int Id, Action Pressed);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
