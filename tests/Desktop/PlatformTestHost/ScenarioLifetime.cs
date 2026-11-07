using System.Windows;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.PlatformTestHost;

/// <summary>
/// The lifetime the platform runs. It reports each step on its own line of standard output, then does what the
/// scenario says. The lines are the protocol the tests read:
/// <c>created application=True|False</c> (the lifetime was built),
/// <c>started application=True|False ui-thread=True|False</c> (Start was called),
/// <c>stopped</c> (Stop was called), and the scenario's own lines.
/// </summary>
internal sealed class ScenarioLifetime : IAppLifetime
{
    private readonly Action<Action> _whatStartDoes;

    public ScenarioLifetime(Action<Action> whatStartDoes)
    {
        _whatStartDoes = whatStartDoes;
        Say($"created application={Application.Current is not null}");
    }

    public static void Say(string line) => Console.Out.WriteLine(line);

    public void Start(Action endApp)
    {
        var application = Application.Current;
        Say($"started application={application is not null} ui-thread={application?.Dispatcher.CheckAccess()}");
        _whatStartDoes(endApp);
    }

    public void Stop() => Say("stopped");
}
