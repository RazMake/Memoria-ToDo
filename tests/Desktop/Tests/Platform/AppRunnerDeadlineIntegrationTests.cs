using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Integration]
[Collection(ScreenTests.Collection)]
public class AppRunnerDeadlineIntegrationTests
{
    private static readonly TimeSpan ExitLimit = TimeSpan.FromSeconds(12);

    [Theory]
    [InlineData("stop-stalls")]
    [InlineData("stop-blocks-ui")]
    [InlineData("blocked-ui-end-from-thread")]
    public void A_hung_cleanup_or_UI_thread_cannot_prevent_exit(string scenario)
    {
        using var host = HostProcess.Start(scenario);

        Assert.Equal(1, host.WaitForExit(ExitLimit));

        Assert.Contains(host.LogEntries, entry => entry.Level == LogEventLevel.Fatal);
        Assert.Contains("1", host.LogEntries.Last(entry => entry.Level == LogEventLevel.Information).Message.Replace(AppVersion.Value, ""));
        if (scenario == "blocked-ui-end-from-thread")
        {
            Assert.Contains("background-end-returned", host.Lines);
        }
        else
        {
            Assert.Equal(1, host.Count("stopped"));
        }
        host.AssertNoOtherMessageBox();
    }

    [Theory]
    [InlineData("stop-stalls-exit-handler-hangs")]
    [InlineData("clean-stop-exit-handler-hangs")]
    public void An_exit_handler_cannot_keep_the_process_alive_even_after_normal_cleanup(string scenario)
    {
        using var host = HostProcess.Start(scenario);

        Assert.Equal(1, host.WaitForExit(ExitLimit));

        Assert.Contains("exit-handler-entered", host.Lines);
        Assert.Contains(host.LogEntries, entry => entry.Level == LogEventLevel.Fatal);
        host.AssertNoOtherMessageBox();
    }

    [Fact]
    public void A_starved_thread_pool_cannot_block_either_exit_or_its_termination_backstop()
    {
        using var host = HostProcess.Start("starved-pool-exit-handler-hangs");

        Assert.Equal(1, host.WaitForExit(ExitLimit));

        Assert.Contains("exit-handler-entered", host.Lines);
        Assert.Contains(host.LogEntries, entry => entry.Level == LogEventLevel.Fatal);
        host.AssertNoOtherMessageBox();
    }

    [Fact]
    public void A_cancelled_cleanup_is_logged_and_exits_without_a_dialog()
    {
        using var host = HostProcess.Start("stop-cancelled");

        Assert.Equal(1, host.WaitForExit(ExitLimit));

        Assert.Contains(host.LogEntries, entry => entry.Level == LogEventLevel.Error && entry.Exception.Length > 0);
        Assert.Contains(host.LogEntries, entry => entry.Level == LogEventLevel.Fatal && entry.Exception.Length == 0);
        Assert.Contains("stop-resource available=True", host.Lines);
        host.AssertNoOtherMessageBox();
    }
}
