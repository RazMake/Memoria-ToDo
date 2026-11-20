using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// What running the app logs, and what the app does after an error nobody handles, each scenario a run of the
/// platform's test program, which prints each entry the platform logs. Entries are checked by level and the facts they
/// name, never by wording.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class AppRunnerLoggingIntegrationTests
{
    [Fact]
    public void Run_logs_the_start_with_the_version_and_the_end_with_the_exit_code_at_Information()
    {
        using var host = HostProcess.Start("end-app");

        var exitCode = host.WaitForExit();

        Assert.Equal(0, exitCode);
        var entries = host.LogEntries.Where(e => e.Level == LogEventLevel.Information).ToList();
        Assert.Contains(entries, e => e.Names(AppVersion.Value));
        var end = entries.Last();
        Assert.Contains("0", end.Message.Replace(AppVersion.Value, ""));
        Assert.DoesNotContain(host.LogEntries, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void Run_logs_the_end_with_the_non_zero_exit_code_when_an_error_ended_the_app()
    {
        using var host = HostProcess.Start("start-throws");

        host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.Equal(1, exitCode);
        var end = host.LogEntries.Last(e => e.Level == LogEventLevel.Information);
        Assert.Contains("1", end.Message.Replace(AppVersion.Value, ""));
    }

    [Fact]
    public void Run_logs_an_error_on_the_UI_thread_after_start_at_Error_with_its_exception_and_shows_it_in_a_box()
    {
        using var host = HostProcess.Start("ui-error");

        var text = host.CloseMessageBox();
        host.TellToEndApp();
        host.WaitForExit();

        Assert.Contains("ui boom", text);
        var logged = host.LogEntries.Where(e => e.Names("ui boom")).ToList();
        var entry = Assert.Single(logged);
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.Contains("ui boom", entry.Exception);
        Assert.DoesNotContain(host.LogEntries, e => e.Level == LogEventLevel.Fatal);
    }

    [Fact]
    public void Run_keeps_the_app_running_after_an_error_on_the_UI_thread_after_start_and_stops_the_lifetime_once_when_told_to_end()
    {
        using var host = HostProcess.Start("ui-error");

        host.CloseMessageBox();
        host.TellToEndApp();
        var exitCode = host.WaitForExit();

        var lines = host.Lines.ToList();
        Assert.True(lines.IndexOf("still-running") >= 0, string.Join(" | ", lines));
        Assert.True(lines.IndexOf("still-running") < lines.IndexOf("stopped"), string.Join(" | ", lines));
        Assert.Equal(1, host.Count("stopped"));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Run_logs_a_second_error_on_the_UI_thread_while_the_first_ones_box_is_open_without_a_second_box()
    {
        using var host = HostProcess.Start("ui-error-twice");

        var text = host.CloseMessageBox();
        host.TellToEndApp();
        host.WaitForExit();

        Assert.Contains("first boom", text);
        Assert.Equal(LogEventLevel.Error, Assert.Single(host.LogEntries, e => e.Names("first boom")).Level);
        Assert.Equal(LogEventLevel.Error, Assert.Single(host.LogEntries, e => e.Names("second boom")).Level);
        Assert.Equal(1, host.Count("stopped"));
        host.AssertNoOtherMessageBox();
    }

    [Fact]
    public void Run_logs_an_error_while_starting_at_Fatal_with_its_exception_stops_the_lifetime_once_and_returns_1()
    {
        using var host = HostProcess.Start("start-throws");

        var text = host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.Contains("start boom", text);
        var entry = Assert.Single(host.LogEntries, e => e.Names("start boom"));
        Assert.Equal(LogEventLevel.Fatal, entry.Level);
        Assert.Contains("start boom", entry.Exception);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Run_logs_an_error_while_stopping_at_Fatal_with_its_exception_and_returns_1()
    {
        using var host = HostProcess.Start("stop-throws");

        host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        var entry = Assert.Single(host.LogEntries, e => e.Names("stop boom"));
        Assert.Equal(LogEventLevel.Fatal, entry.Level);
        Assert.Contains("stop boom", entry.Exception);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Run_logs_an_error_on_the_UI_thread_while_stopping_at_Fatal_shows_it_and_returns_1()
    {
        using var host = HostProcess.Start("ui-error-during-stop");

        var text = host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.Contains("stop ui boom", text);
        var entry = Assert.Single(host.LogEntries, e => e.Names("stop ui boom"));
        Assert.Equal(LogEventLevel.Fatal, entry.Level);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Run_logs_an_error_on_another_thread_at_Fatal_with_its_exception_before_the_process_ends_with_1()
    {
        using var host = HostProcess.Start("thread-error");

        var text = host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        Assert.Contains("thread boom", text);
        var entry = Assert.Single(host.LogEntries, e => e.Names("thread boom"));
        Assert.Equal(LogEventLevel.Fatal, entry.Level);
        Assert.Contains("thread boom", entry.Exception);
        Assert.Equal(1, host.Count("stopped"));
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Run_lets_the_lifetimes_asynchronous_stop_finish_after_an_error_on_another_thread_before_it_logs_the_end_and_returns_1()
    {
        using var host = HostProcess.Start("thread-error-async-stop");

        host.CloseMessageBox();
        var exitCode = host.WaitForExit();

        var output = host.Output.ToList();
        var stopCompleted = output.FindIndex(line => line.StartsWith("async-stop-completed", StringComparison.Ordinal));
        var end = output.FindLastIndex(line => line.StartsWith(HostLogEntry.LinePrefix + LogEventLevel.Information, StringComparison.Ordinal));
        Assert.True(stopCompleted >= 0, string.Join(" | ", output));
        Assert.True(stopCompleted < end, string.Join(" | ", output));
        Assert.Contains("1", host.LogEntries.Last(e => e.Level == LogEventLevel.Information).Message.Replace(AppVersion.Value, ""));
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Run_logs_an_unobserved_task_exception_at_Error_marks_it_observed_shows_no_box_and_keeps_the_app_running()
    {
        using var host = HostProcess.Start("unobserved-task");

        var exitCode = host.WaitForExit();

        var entry = Assert.Single(host.LogEntries, e => e.Names("unobserved boom"));
        Assert.Equal(LogEventLevel.Error, entry.Level);
        Assert.Contains("unobserved boom", entry.Exception);
        Assert.Contains("unobserved-observed=True", host.Lines);
        var lines = host.Lines.ToList();
        Assert.True(lines.IndexOf("still-running") >= 0, string.Join(" | ", lines));
        Assert.True(lines.IndexOf("still-running") < lines.IndexOf("stopped"), string.Join(" | ", lines));
        Assert.Equal(0, exitCode);
        host.AssertNoOtherMessageBox();
    }
}
