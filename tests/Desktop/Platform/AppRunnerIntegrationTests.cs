using Xunit;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Run the app, each scenario a run of the platform's test program.</summary>
[Integration]
public class AppRunnerIntegrationTests
{
    [Fact]
    public void Run_creates_the_WPF_application_before_building_the_lifetime()
    {
        using var host = HostProcess.Start("end-app");

        host.WaitForExit();

        Assert.Contains("created application=True", host.Lines);
    }

    [Fact]
    public void Run_starts_the_lifetime_on_the_UI_thread_after_the_WPF_application_exists()
    {
        using var host = HostProcess.Start("end-app");

        host.WaitForExit();

        Assert.Contains("started application=True ui-thread=True", host.Lines);
    }

    [Fact]
    public void Run_sets_the_theme_to_follow_Windows_before_building_the_lifetime()
    {
        using var host = HostProcess.Start("end-app");

        host.WaitForExit();

        Assert.Contains("created theme-follows-windows=True", host.Lines);
    }

    [Fact]
    public void Run_builds_the_lifetime_before_starting_it()
    {
        using var host = HostProcess.Start("end-app");

        host.WaitForExit();

        Assert.Equal(["created application=True"], host.Lines.Take(1));
        Assert.Equal(1, host.Count("started application=True ui-thread=True"));
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

    [Fact]
    public void Run_stops_the_lifetime_once_and_returns_zero_when_the_lifetime_ends_the_app()
    {
        using var host = HostProcess.Start("end-app");

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
    }

    [Fact]
    public void Run_stops_the_lifetime_while_its_message_window_is_still_usable()
    {
        using var host = HostProcess.Start("end-app");

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Contains("stop-resource available=True", host.Lines);
    }

    [Theory]
    [InlineData("end-app-from-background-thread")]
    [InlineData("session-ending")]
    public void Run_keeps_the_lifetimes_message_window_usable_when_an_external_request_ends_the_app(string scenario)
    {
        using var host = HostProcess.Start(scenario);

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Contains("stop-resource available=True", host.Lines);
    }

    [Fact]
    public void Run_stops_the_lifetime_once_and_returns_zero_when_a_background_thread_ends_the_app()
    {
        using var host = HostProcess.Start("end-app-from-background-thread");

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Equal(1, host.Count("stopped"));
    }

    [Fact]
    public void Run_stops_the_lifetime_once_and_returns_zero_when_Windows_ends_the_session()
    {
        using var host = HostProcess.Start("session-ending");

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        Assert.Contains("session-ending-sent", host.Lines);
        Assert.Equal(1, host.Count("stopped"));
    }
}
