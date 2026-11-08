using System.Windows;
using System.Windows.Interop;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.PlatformTestHost;

/// <summary>
/// The lifetime the platform runs. It reports each step on its own line of standard output, then does what the
/// scenario says. The lines are the protocol the tests read:
/// <c>created application=True|False</c> (the lifetime was built),
/// <c>created theme-follows-windows=True|False</c> (the application's theme followed Windows when it was built),
/// <c>started application=True|False ui-thread=True|False</c> (Start was called),
/// <c>stop-resource available=True|False</c> (the message window was still usable when Stop ran),
/// <c>stopped</c> (Stop was called), and the scenario's own lines.
/// </summary>
internal sealed class ScenarioLifetime : IAppLifetime
{
    private readonly Action<Action> _whatStartDoes;
    private readonly Action _whatStopDoes;
    private HwndSource? _messageWindow;

    public ScenarioLifetime(Action<Action> whatStartDoes, Action whatStopDoes)
    {
        _whatStartDoes = whatStartDoes;
        _whatStopDoes = whatStopDoes;
        Say($"created application={Application.Current is not null}");
#pragma warning disable WPF0001 // Application.ThemeMode is experimental; allowed only on this line.
        var followsWindows = Application.Current?.ThemeMode == ThemeMode.System;
#pragma warning restore WPF0001
        Say($"created theme-follows-windows={followsWindows}");
    }

    public static void Say(string line) => Console.Out.WriteLine(line);

    public void Start(Action endApp)
    {
        var application = Application.Current;
        Say($"started application={application is not null} ui-thread={application?.Dispatcher.CheckAccess()}");
        _messageWindow = new HwndSource(new HwndSourceParameters("Platform test lifetime")
        {
            ParentWindow = -3, // HWND_MESSAGE: the same kind of resource the global shortcuts own.
        });
        _whatStartDoes(endApp);
    }

    public void Stop()
    {
        if (_messageWindow is not null)
        {
            Say($"stop-resource available={!_messageWindow.IsDisposed}");
            _messageWindow.Dispose();
        }

        Say("stopped");
        _whatStopDoes();
    }
}
