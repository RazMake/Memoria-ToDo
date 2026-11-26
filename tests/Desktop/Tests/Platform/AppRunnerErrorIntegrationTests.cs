using Xunit;
using Memoria.ToDo.Testing;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Run the app when an error nobody handles happens, each scenario a run of the platform's test program.</summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class AppRunnerErrorIntegrationTests
{
    [Fact]
    public void Run_reports_a_shutdown_failure_after_an_await_and_still_closes_resources()
    {
        using var host = HostProcess.Start("async-stop-throws");

        var exitCode = host.WaitForExit();

        Assert.Contains(host.LogEntries, entry => entry.Level == LogEventLevel.Error && entry.Names("async stop boom"));
        host.AssertNoOtherMessageBox();
        Assert.NotEqual(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Contains("async-stop-completed ui-thread=True", host.Lines);
        Assert.Contains("stop-resource available=True", host.Lines);
    }

    [Theory]
    [InlineData("thread-error", "thread boom")]
    [InlineData("start-throws", "start boom")]
    public void Run_shows_an_unhandled_error_in_a_message_box_then_stops_the_lifetime_once_with_its_message_window_usable_and_returns_non_zero(
        string scenario, string message)
    {
        using var host = HostProcess.Start(scenario);

        var text = host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.Contains(message, text);
        Assert.NotEqual(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Contains("stop-resource available=True", host.Lines);
    }

    [Fact]
    public void Run_reports_a_failed_stop_once_and_returns_non_zero()
    {
        using var host = HostProcess.Start("stop-throws");

        var exitCode = host.WaitForExit();

        Assert.Contains(host.LogEntries, entry => entry.Level == LogEventLevel.Error && entry.Names("stop boom"));
        host.AssertNoOtherMessageBox();
        Assert.NotEqual(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Contains("stop-resource available=True", host.Lines);
    }

    [Fact]
    public void Run_reports_a_failed_stop_after_an_error_on_another_thread()
    {
        using var host = HostProcess.Start("thread-error-stop-throws");

        var originalError = host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.Contains("thread boom", originalError);
        Assert.Contains(host.LogEntries, entry => entry.Level == LogEventLevel.Error && entry.Names("stop boom"));
        host.AssertNoOtherMessageBox();
        Assert.NotEqual(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Contains("stop-resource available=True", host.Lines);
    }
}
