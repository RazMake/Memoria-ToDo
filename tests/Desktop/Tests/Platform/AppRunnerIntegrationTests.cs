using Xunit;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Run the app, each scenario a run of the platform's test program.</summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class AppRunnerIntegrationTests
{
    [Fact]
    public void Run_builds_the_lifetime_after_the_WPF_application_and_theme_exist_then_starts_and_stops_it_once_and_returns_zero_when_the_lifetime_ends_the_app()
    {
        using var host = HostProcess.Start("end-app");

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Equal(["created application=True"], host.Lines.Take(1));
        Assert.Contains("created theme-follows-windows=True", host.Lines);
        Assert.Equal(1, host.Count("started application=True ui-thread=True"));
        Assert.Equal(1, host.Count("stopped"));
        Assert.Contains("stop-resource available=True", host.Lines);
    }

    [Theory]
    [InlineData("async-stop")]
    [InlineData("end-app-twice")]
    public void Run_keeps_the_dispatcher_alive_until_async_shutdown_completes_once(string scenario)
    {
        using var host = HostProcess.Start(scenario);

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Equal(1, host.Count("async-stop-completed ui-thread=True"));
        Assert.Contains("stop-resource available=True", host.Lines);
    }

    [Fact]
    public void Run_does_not_refuse_Windows_logoff_while_async_cleanup_is_pending()
    {
        using var host = HostProcess.Start("session-ending-async-stop");

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.DoesNotContain("session-ending-refused", host.Lines);
    }

    [Fact]
    public void Run_keeps_the_app_running_when_the_last_window_closes()
    {
        using var host = HostProcess.Start("last-window-closed");

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        var window = host.Lines.ToList().IndexOf("window-closed");
        var running = host.Lines.ToList().IndexOf("still-running");
        var stopped = host.Lines.ToList().IndexOf("stopped");
        Assert.True(window >= 0 && window < running && running < stopped, string.Join(" | ", host.Lines));
    }

    [Theory]
    [InlineData("end-app-from-background-thread", false)]
    [InlineData("session-ending", true)]
    public void Run_stops_the_lifetime_once_and_returns_zero_with_its_message_window_usable_when_an_external_request_ends_the_app(
        string scenario, bool windowsEndsTheSession)
    {
        using var host = HostProcess.Start(scenario);

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Contains("stop-resource available=True", host.Lines);
        Assert.Equal(windowsEndsTheSession, host.Lines.Contains("session-ending-sent"));
    }
}
