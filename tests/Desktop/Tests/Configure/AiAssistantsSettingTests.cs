using System.IO;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.MCPServer;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The AI assistants decision behind the card: whether the switch is on, the status line, the lines to copy, and what it says
/// when switching or copying fails. It shows what the MCP server's status says and passes the user's switch and copies on.
/// </summary>
[Unit]
public class AiAssistantsSettingTests
{
    private readonly FakeMcpServerStatus server = new();
    private readonly FakeSetupActions actions = new();
    private readonly InMemoryLogSink log = new();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Starts_from_the_servers_switch_and_changes_nothing(bool on)
    {
        server.State = new McpServerState(on, on, null);

        var setting = Create();

        Assert.Equal(on, setting.IsOn);
        Assert.Null(setting.Problem);
        Assert.Empty(server.SetOnCalls);
        Assert.Empty(actions.Calls);
    }

    [Fact]
    public void Says_running_while_the_server_listens()
    {
        server.State = new McpServerState(true, true, null);

        var setting = Create();

        Assert.Equal("Running. AI assistants on this PC can use Memoria-ToDo.", setting.Status);
        Assert.False(setting.StatusIsFailure);
    }

    [Fact]
    public void Says_starting_while_the_server_is_on_and_not_listening_yet()
    {
        server.State = new McpServerState(true, false, null);

        var setting = Create();

        Assert.Equal("Starting…", setting.Status);
        Assert.False(setting.StatusIsFailure);
    }

    [Fact]
    public void Says_off_while_the_server_is_off()
    {
        server.State = new McpServerState(false, false, null);

        var setting = Create();

        Assert.Equal("Off. AI assistants can't reach Memoria-ToDo.", setting.Status);
        Assert.False(setting.StatusIsFailure);
    }

    [Fact]
    public void Shows_the_servers_failure_as_the_status_in_the_error_color()
    {
        server.State = new McpServerState(true, false, "The pipe is taken by another program.");

        var setting = Create();

        Assert.Equal("The pipe is taken by another program.", setting.Status);
        Assert.True(setting.StatusIsFailure);
        Assert.True(setting.IsOn);
    }

    [Fact]
    public void Shows_the_failure_of_a_switch_that_cannot_be_read_while_the_server_counts_as_off()
    {
        server.State = new McpServerState(false, false, "The switch file can't be read.");

        var setting = Create();

        Assert.Equal("The switch file can't be read.", setting.Status);
        Assert.True(setting.StatusIsFailure);
        Assert.False(setting.IsOn);
    }

    [Fact]
    public void Follows_the_server_when_its_state_changes()
    {
        server.State = new McpServerState(true, false, null);
        var setting = Create();

        server.Publish(new McpServerState(true, true, null));
        var running = setting.Status;
        server.Publish(new McpServerState(false, false, null));

        Assert.Equal("Running. AI assistants on this PC can use Memoria-ToDo.", running);
        Assert.Equal("Off. AI assistants can't reach Memoria-ToDo.", setting.Status);
        Assert.False(setting.IsOn);
    }

    [Fact]
    public void Lists_the_assistants_the_server_lists_in_its_order()
    {
        McpConnection[] connections = [new("VS Code", "vs"), new("Claude Desktop", "cd"), new("Codex", "cx")];
        server.Connections = connections;

        var setting = Create();

        Assert.Equal(connections, setting.Connections);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Switching_calls_the_server_once_at_once_and_shows_the_new_value(bool on)
    {
        server.State = new McpServerState(!on, !on, null);
        var setting = Create();

        setting.Set(on);

        Assert.Equal([on], server.SetOnCalls);
        Assert.Equal(on, setting.IsOn);
        Assert.Null(setting.Problem);
    }

    [Fact]
    public void A_failed_switch_on_keeps_the_switch_off_and_shows_the_errors_message()
    {
        server.State = new McpServerState(false, false, null);
        server.SetOnFailure = new IOException("The switch file is read-only.");
        var setting = Create();

        setting.Set(true);

        Assert.False(setting.IsOn);
        Assert.Equal("The switch file is read-only.", setting.Problem);
        Assert.Equal([true], server.SetOnCalls);
    }

    [Fact]
    public void A_failed_switch_off_keeps_the_switch_on_and_shows_the_errors_message()
    {
        server.SetOnFailure = new IOException("The switch file is read-only.");
        var setting = Create();

        setting.Set(false);

        Assert.True(setting.IsOn);
        Assert.Equal("The switch file is read-only.", setting.Problem);
    }

    [Fact]
    public void A_failed_switch_does_not_change_the_status_line()
    {
        server.SetOnFailure = new IOException("The switch file is read-only.");
        var setting = Create();

        setting.Set(false);

        Assert.Equal("Running. AI assistants on this PC can use Memoria-ToDo.", setting.Status);
        Assert.False(setting.StatusIsFailure);
    }

    [Fact]
    public void A_failed_switch_is_logged_as_an_error_with_the_exception()
    {
        var failure = new IOException("The switch file is read-only.");
        server.SetOnFailure = failure;
        var setting = Create();

        setting.Set(false);

        Assert.Contains(log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));
    }

    [Fact]
    public void A_later_successful_switch_clears_the_problem()
    {
        server.SetOnFailure = new IOException("The switch file is read-only.");
        var setting = Create();
        setting.Set(false);
        server.SetOnFailure = null;

        setting.Set(false);

        Assert.False(setting.IsOn);
        Assert.Null(setting.Problem);
    }

    [Fact]
    public void Copy_copies_that_assistants_lines_and_nothing_else()
    {
        McpConnection[] connections = [new("VS Code", "vs lines"), new("Codex", "codex lines")];
        server.Connections = connections;
        var setting = Create();

        setting.Copy(connections[1]);

        Assert.Equal(["codex lines"], actions.CopiedTexts);
        Assert.Null(setting.CopyProblem);
        Assert.Empty(server.SetOnCalls);
    }

    [Fact]
    public void A_failed_copy_shows_the_errors_message_apart_from_the_switchs_problem_and_keeps_the_switch()
    {
        actions.CopyFailure = new InvalidOperationException("Clipboard is busy.");
        var setting = Create();

        setting.Copy(new McpConnection("Codex", "codex lines"));

        Assert.Equal("Clipboard is busy.", setting.CopyProblem);
        Assert.Null(setting.Problem);
        Assert.True(setting.IsOn);
        Assert.Empty(actions.CopiedTexts);
    }

    [Fact]
    public void A_failed_copy_is_logged_as_an_error_with_the_exception()
    {
        var failure = new InvalidOperationException("Clipboard is busy.");
        actions.CopyFailure = failure;
        var setting = Create();

        setting.Copy(new McpConnection("Codex", "codex lines"));

        Assert.Contains(log.Events, entry => entry.Level == LogEventLevel.Error && entry.HasException(failure));
    }

    [Fact]
    public void A_later_successful_copy_clears_the_copy_problem()
    {
        actions.CopyFailure = new InvalidOperationException("Clipboard is busy.");
        var setting = Create();
        var connection = new McpConnection("Codex", "codex lines");
        setting.Copy(connection);
        actions.CopyFailure = null;

        setting.Copy(connection);

        Assert.Null(setting.CopyProblem);
        Assert.Equal(["codex lines"], actions.CopiedTexts);
    }

    [Fact]
    public void Working_calls_log_no_problem()
    {
        var setting = Create();

        setting.Set(false);
        setting.Copy(new McpConnection("Codex", "codex lines"));

        log.Events.AssertNoProblems();
    }

    [Fact]
    public void Rejects_a_missing_server()
    {
        Assert.Throws<ArgumentNullException>(() => new AiAssistantsSetting(null!, actions, Logger.None));
    }

    [Fact]
    public void Rejects_missing_setup_actions()
    {
        Assert.Throws<ArgumentNullException>(() => new AiAssistantsSetting(server, null!, Logger.None));
    }

    [Fact]
    public void Rejects_a_missing_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new AiAssistantsSetting(server, actions, null!));
    }

    private AiAssistantsSetting Create() => new(server, actions, log.CreateLogger());
}
