using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.McpServer.Tests;

/// <summary>
/// The on/off switch kept in mcp-server.txt, and the state the server reports. Unit tests never listen on a pipe, so they cover
/// the off and failure cases; the integration tests cover listening.
/// </summary>
[Unit]
public class McpSwitchTests
{
    [WpfFact]
    public void Stays_off_and_listens_on_no_pipe_when_the_file_says_off()
    {
        var app = new McpHarness();
        app.StartOff();

        Assert.False(app.Server.State.IsOn);
        Assert.False(app.Server.State.Listening);
        Assert.Null(app.Server.State.Failure);
    }

    [WpfTheory]
    [InlineData("maybe")]
    [InlineData("")]
    public void Treats_any_other_content_as_off_with_a_Warning_and_a_failure_to_show(string content)
    {
        var app = new McpHarness();
        app.WriteSwitch(content);

        app.Server.Start();

        Assert.False(app.Server.State.IsOn);
        Assert.False(app.Server.State.Listening);
        Assert.False(string.IsNullOrWhiteSpace(app.Server.State.Failure));
        app.Log.Events.At(LogEventLevel.Warning).Single();
    }

    [WpfFact]
    public void Treats_an_unreadable_file_as_off_with_a_Warning_and_a_failure_to_show()
    {
        var app = new McpHarness();
        app.WriteSwitch("on");
        app.Files.FailReadsUnder(McpHarness.DeviceFolder);

        app.Server.Start();

        Assert.False(app.Server.State.IsOn);
        Assert.False(app.Server.State.Listening);
        Assert.False(string.IsNullOrWhiteSpace(app.Server.State.Failure));
        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Warning));
    }

    [WpfFact]
    public void Reports_not_listening_and_no_failure_before_it_starts()
    {
        var app = new McpHarness();

        Assert.False(app.Server.State.Listening);
        Assert.Null(app.Server.State.Failure);
        Assert.Empty(app.Log.Events.At(LogEventLevel.Warning));
    }

    // Before Start, SetOn only saves the choice: listening begins at Start, so no pipe is opened here.
    [Theory]
    [InlineData(true, "on")]
    [InlineData(false, "off")]
    public async Task SetOn_writes_the_choice_to_the_file_and_logs_it_at_Information(bool on, string expected)
    {
        await using var app = new McpHarness();

        app.Server.SetOn(on);

        Assert.Equal(expected, app.ReadSwitch());
        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Information));
        app.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public void SetOn_off_keeps_the_server_off_and_raises_Changed_once_on_the_UI_thread()
    {
        var app = new McpHarness();
        app.StartOff();
        var ui = Environment.CurrentManagedThreadId;
        var threads = new List<int>();
        app.Server.Changed += (_, _) => threads.Add(Environment.CurrentManagedThreadId);

        app.Server.SetOn(false);

        Assert.False(app.Server.State.IsOn);
        Assert.Equal(new[] { ui }, threads);
    }

    [WpfFact]
    public void SetOn_off_when_already_off_still_writes_the_file()
    {
        var app = new McpHarness();
        app.WriteSwitch("maybe");
        app.Server.Start();

        app.Server.SetOn(false);

        Assert.Equal("off", app.ReadSwitch());
        Assert.Null(app.Server.State.Failure);
    }

    [WpfFact]
    public void SetOn_throws_and_changes_nothing_when_the_file_cannot_be_written()
    {
        var app = new McpHarness();
        app.StartOff();
        var raised = 0;
        app.Server.Changed += (_, _) => raised++;
        app.Files.FailWritesUnder(McpHarness.DeviceFolder);

        var failure = Assert.Throws<IOException>(() => app.Server.SetOn(true));

        Assert.Contains("Writing is not allowed here", failure.Message);
        Assert.False(app.Server.State.IsOn);
        Assert.Equal("off", app.ReadSwitch());
        Assert.Equal(0, raised);
    }

    [WpfFact]
    public async Task Stop_after_a_start_with_the_switch_off_completes_and_leaves_the_state_off()
    {
        var app = new McpHarness();
        app.StartOff();

        await app.Server.StopAsync().WaitAsync(McpHarness.Bound);

        Assert.False(app.Server.State.Listening);
        Assert.False(app.Server.State.IsOn);
    }
}
