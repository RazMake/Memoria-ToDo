using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.PlatformTestHost;

/// <summary>
/// The scenarios, by the name the tests pass as the argument. Each is what Start does, given the way to end
/// the app. A new scenario is a new entry.
/// </summary>
internal static class Scenarios
{
    // The two messages Windows sends a window when the user signs out or shuts down.
    private const int WmQueryEndSession = 0x0011;
    private const int WmEndSession = 0x0016;
    private static readonly nint EndSessionLogoff = unchecked((nint)0x80000000);

    public static IReadOnlyDictionary<string, Action<Action>> All { get; } = new Dictionary<string, Action<Action>>
    {
        // Start asks to end the app, as Shell does for the tray's Exit.
        ["end-app"] = endApp => Post(endApp),

        // Start asks to end the app from a background thread, as a module's background task may.
        ["end-app-from-background-thread"] = endApp => new Thread(() => endApp()).Start(),

        // Start opens a window and closes it again, then ends the app once the app has had time to react.
        ["last-window-closed"] = endApp =>
        {
            var window = new Window();
            window.Show();
            window.Close();
            ScenarioLifetime.Say("window-closed");
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                ScenarioLifetime.Say("still-running");
                endApp();
            });
        },

        // Start sends the messages Windows sends when the session ends to every top-level window of the UI thread,
        // as Windows does. WPF raises SessionEnding only from the Application's own hidden window, not from a new Window.
        ["session-ending"] = _ => Post(() =>
        {
            var windows = new List<nint>();
            EnumThreadWindows(GetCurrentThreadId(), (window, _) =>
            {
                windows.Add(window);
                return true;
            }, 0);
            if (windows.Count == 0)
            {
                throw new InvalidOperationException("The UI thread has no top-level window to send the session-ending messages to.");
            }

            ScenarioLifetime.Say("session-ending-sent");
            foreach (var window in windows)
            {
                SendMessage(window, WmQueryEndSession, 0, EndSessionLogoff);
            }

            foreach (var window in windows)
            {
                SendMessage(window, WmEndSession, 1, EndSessionLogoff);
            }
        }),

        // An error nobody handles on the UI thread, after Start finished.
        ["ui-error"] = _ => Post(() => throw new InvalidOperationException("ui boom")),

        // An error nobody handles on another thread.
        ["thread-error"] = _ => new Thread(() => throw new InvalidOperationException("thread boom")).Start(),

        // Start itself fails, as when the tray icon can't be added.
        ["start-throws"] = _ => throw new InvalidOperationException("start boom"),
    };

    private static void Post(Action action) => Application.Current.Dispatcher.BeginInvoke(action);

    private delegate bool EnumWindowsProc(nint window, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
