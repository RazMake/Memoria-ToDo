using Xunit;
using Memoria.ToDo.Testing;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>Run the app when an error nobody handles happens, each scenario a run of the platform's test program.</summary>
[Integration]
public class AppRunnerErrorIntegrationTests
{
    [Fact]
    public void Run_shows_an_unhandled_error_on_the_UI_thread_in_a_message_box()
    {
        using var host = HostProcess.Start("ui-error");

        var text = host.CloseMessageBox();
        host.WaitForExit();

        Assert.Contains("ui boom", text);
    }

    [Fact]
    public void Run_returns_non_zero_after_an_unhandled_error_on_the_UI_thread()
    {
        using var host = HostProcess.Start("ui-error");

        host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Run_stops_a_started_lifetime_once_after_an_unhandled_error_on_the_UI_thread()
    {
        using var host = HostProcess.Start("ui-error");

        host.CloseMessageBox();
        host.WaitForExit();

        Assert.Equal(1, host.Count("stopped"));
    }

    [Fact]
    public void Run_shows_an_unhandled_error_on_another_thread_in_a_message_box()
    {
        using var host = HostProcess.Start("thread-error");

        var text = host.CloseMessageBox();
        host.WaitForExit();

        Assert.Contains("thread boom", text);
    }

    [Fact]
    public void Run_ends_with_non_zero_after_an_unhandled_error_on_another_thread()
    {
        using var host = HostProcess.Start("thread-error");

        host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.NotEqual(0, exitCode);
    }

    [Fact]
    public void Run_shows_an_error_thrown_by_Start_in_a_message_box()
    {
        using var host = HostProcess.Start("start-throws");

        var text = host.CloseMessageBox();
        host.WaitForExit();

        Assert.Contains("start boom", text);
    }

    [Fact]
    public void Run_returns_non_zero_when_Start_throws()
    {
        using var host = HostProcess.Start("start-throws");

        host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.NotEqual(0, exitCode);
        Assert.True(host.Count("stopped") <= 1, "The lifetime must be stopped at most once.");
    }
}
