using Memoria.ToDo.Desktop.Configure;

namespace Memoria.ToDo.Desktop.App;

/// <summary>
/// The Start with Windows steps of Velopack's install and uninstall hooks. Fast, and they show no window. A step
/// that fails goes on quietly, since it can't stop the install or uninstall, and reports its failure.
/// </summary>
internal sealed class StartWithWindowsSteps
{
    private readonly IStartWithWindows _startWithWindows;
    private readonly Action<Exception> _reportFailure;

    public StartWithWindowsSteps(IStartWithWindows startWithWindows, Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(startWithWindows);
        ArgumentNullException.ThrowIfNull(reportFailure);
        _startWithWindows = startWithWindows;
        _reportFailure = reportFailure;
    }

    /// <summary>Turns the entry on. After a failed write, Configure shows Start with Windows off.</summary>
    public void Install() => Apply(on: true);

    /// <summary>Removes the entry. A failed removal leaves the entry.</summary>
    public void Uninstall() => Apply(on: false);

    private void Apply(bool on)
    {
        try
        {
            _startWithWindows.Set(on);
        }
        catch (InvalidOperationException failure)
        {
            _reportFailure(failure);
        }
    }
}