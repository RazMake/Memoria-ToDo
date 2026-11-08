using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The install and uninstall steps the composition root gives to Velopack's hooks, <c>StartWithWindowsSteps</c>
/// with its <c>Install</c> and <c>Uninstall</c>. They run on the real Run key adapter over a scratch key. Velopack
/// itself isn't started, so each test collects the failures a step reports.
/// </summary>
[Integration]
public class StartWithWindowsStepsTests
{
    private const string Starter = @"C:\Test Install\MemoriaToDo\Memoria-ToDo.exe";

    private readonly List<Exception> reported = [];

    [Fact]
    public void The_install_step_writes_the_entry_and_reports_nothing()
    {
        using var key = new ScratchRunKey();
        var steps = new StartWithWindowsSteps(new RunKeyStartWithWindows(key.Path, Starter), reported.Add);

        steps.Install();

        Assert.Contains(Starter, key.OnlyValue());
        Assert.Empty(reported);
    }

    [Fact]
    public void The_uninstall_step_removes_the_entry_and_reports_nothing()
    {
        using var key = new ScratchRunKey();
        var port = new RunKeyStartWithWindows(key.Path, Starter);
        port.Set(true);
        var steps = new StartWithWindowsSteps(port, reported.Add);

        steps.Uninstall();

        Assert.Empty(key.ValueNames());
        Assert.Empty(reported);
    }

    [Fact]
    public void A_failed_write_is_reported_and_does_not_stop_the_install()
    {
        using var key = new ScratchRunKey();
        var steps = new StartWithWindowsSteps(new RunKeyStartWithWindows(key.Path, installedStarterPath: null), reported.Add);

        steps.Install();

        Assert.Empty(key.ValueNames());
        Assert.IsType<InvalidOperationException>(Assert.Single(reported));
    }

    [Fact]
    public void A_failed_removal_is_reported_and_does_not_stop_the_uninstall()
    {
        using var key = new ScratchRunKey();
        new RunKeyStartWithWindows(key.Path, Starter).Set(true);
        var steps = new StartWithWindowsSteps(new RunKeyStartWithWindows(key.Path, installedStarterPath: null), reported.Add);

        steps.Uninstall();

        Assert.Single(key.ValueNames());
        Assert.IsType<InvalidOperationException>(Assert.Single(reported));
    }
}