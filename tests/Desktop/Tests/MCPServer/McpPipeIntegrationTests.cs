using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.MCPServer.Tests;

/// <summary>The server on a real named pipe, as an assistant's bridge reaches it. Every test uses its own pipe name, never the app's.</summary>
[Integration]
public class McpPipeIntegrationTests
{
    private static async Task<NamedPipeClientStream> ConnectPipeAsync(string name, int timeoutMs = 30_000)
    {
        var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeoutMs, TestContext.Current.CancellationToken);
        return pipe;
    }

    private static async Task<McpClient> ClientOnAsync(NamedPipeClientStream pipe) =>
        await McpClient.CreateAsync(new StreamClientTransport(pipe, pipe), cancellationToken: TestContext.Current.CancellationToken).WaitAsync(McpHarness.Bound);

    /// <summary>Releases a held log read when the test ends, so a failed assertion can't leave the server waiting on it.</summary>
    private sealed class ReleaseOnDispose(Action release) : IDisposable
    {
        public void Dispose() => release();
    }

    private static Task WhenChangedAsync(IMcpServer server, Func<McpServerState, bool> condition)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check()
        {
            if (condition(server.State))
            {
                done.TrySetResult();
            }
        }

        server.Changed += (_, _) => Check();
        Check();
        return done.Task.WaitAsync(McpHarness.Bound);
    }

    [WpfFact]
    public async Task Starts_on_and_listening_when_there_is_no_switch_file_and_logs_the_pipe_at_Information()
    {
        await using var app = new McpHarness();

        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);

        Assert.True(app.Server.State.IsOn);
        Assert.Null(app.Server.State.Failure);
        app.Log.Events.SingleAt(LogEventLevel.Information, app.PipeName);
    }

    [WpfFact]
    public async Task Starts_on_and_listening_when_the_file_says_on()
    {
        await using var app = new McpHarness();
        app.WriteSwitch("on");

        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);

        Assert.True(app.Server.State.IsOn);
    }

    [WpfFact]
    public async Task An_assistant_of_this_user_connects_and_lists_the_tools()
    {
        await using var app = new McpHarness();
        app.AddTask("Over the pipe");
        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);
        await using var pipe = await ConnectPipeAsync(app.PipeName);
        await using var client = await ClientOnAsync(pipe);

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken).AsTask().WaitAsync(McpHarness.Bound);
        var answer = (await McpHarness.Call(client, "list_tasks")).Json();

        Assert.Contains(tools, tool => tool.Name == "list_tasks");
        Assert.Equal("Over the pipe", answer.Items("tasks").Single().Str("title"));
    }

    [WpfFact]
    public async Task Serves_two_assistants_at_once_over_the_same_pipe_name()
    {
        await using var app = new McpHarness();
        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);
        await using var firstPipe = await ConnectPipeAsync(app.PipeName);
        await using var secondPipe = await ConnectPipeAsync(app.PipeName);
        await using var first = await ClientOnAsync(firstPipe);
        await using var second = await ClientOnAsync(secondPipe);

        var fromFirst = await McpHarness.Call(first, "get_deletion_delay");
        var fromSecond = await McpHarness.Call(second, "get_deletion_delay");

        Assert.True(fromFirst.IsError != true);
        Assert.True(fromSecond.IsError != true);
    }

    [WpfFact]
    public async Task Logs_a_connection_opening_and_closing_at_Information()
    {
        await using var app = new McpHarness();
        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);
        var before = app.Log.Events.Count;
        var pipe = await ConnectPipeAsync(app.PipeName);
        var client = await ClientOnAsync(pipe);

        await client.DisposeAsync();
        await pipe.DisposeAsync();
        await app.Server.StopAsync().WaitAsync(McpHarness.Bound);

        Assert.True(app.Log.After(before).At(LogEventLevel.Information).Count >= 3, "Expected entries for open, close, and stop.");
        app.Log.Events.AssertNoProblems();
    }

    [WpfFact]
    public async Task Accepts_only_the_current_user_and_refuses_network_clients_by_its_pipe_access_rules()
    {
        await using var app = new McpHarness();
        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);
        await using var pipe = new NamedPipeClientStream(
            ".", app.PipeName, PipeAccessRights.ReadWrite | PipeAccessRights.ReadPermissions, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification, HandleInheritability.None);
        await pipe.ConnectAsync(30_000, TestContext.Current.CancellationToken);

        var rules = pipe.GetAccessControl().GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();

        var me = WindowsIdentity.GetCurrent().User;
        var network = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);
        Assert.Contains(rules, rule => rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference == me);
        Assert.Contains(rules, rule => rule.AccessControlType == AccessControlType.Deny && rule.IdentityReference == network);
        Assert.DoesNotContain(rules, rule => rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference != me);
    }

    [WpfFact]
    public async Task A_pipe_name_another_server_holds_sets_a_failure_and_logs_an_Error_without_throwing()
    {
        await using var app = new McpHarness();
        await using var other = new NamedPipeServerStream(app.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Failure is not null);

        Assert.False(app.Server.State.Listening);
        Assert.False(string.IsNullOrWhiteSpace(app.Server.State.Failure));
        Assert.NotEmpty(app.Log.Events.At(LogEventLevel.Error));
    }

    [WpfFact]
    public async Task Listens_on_no_pipe_while_the_file_says_off()
    {
        await using var app = new McpHarness();
        app.StartOff();

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await using var pipe = await ConnectPipeAsync(app.PipeName, timeoutMs: 1_000);
        });
    }

    [WpfFact]
    public async Task SetOn_false_ends_the_connections_and_stops_listening_then_SetOn_true_listens_again()
    {
        await using var app = new McpHarness();
        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);
        await using var pipe = await ConnectPipeAsync(app.PipeName);
        await using var client = await ClientOnAsync(pipe);

        app.Server.SetOn(false);
        await WhenChangedAsync(app.Server, state => !state.Listening);
        await client.Completion.WaitAsync(McpHarness.Bound);
        app.Server.SetOn(true);
        await WhenChangedAsync(app.Server, state => state.Listening);

        Assert.True(app.Server.State.IsOn);
        await using var again = await ConnectPipeAsync(app.PipeName);
    }

    [WpfFact]
    public async Task Turning_off_then_on_while_a_call_drains_listens_again_serves_a_new_connection_and_lets_the_old_call_finish()
    {
        await using var app = new McpHarness();
        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);
        await using var oldPipe = await ConnectPipeAsync(app.PipeName);
        await using var oldClient = await ClientOnAsync(oldPipe);
        var release = app.Logs.Hold();
        using var releaseOnExit = new ReleaseOnDispose(release);
        var inProgress = McpHarness.Call(oldClient, "read_log");
        await app.Logs.WhenFirstReadStarted().WaitAsync(McpHarness.Bound);

        app.Server.SetOn(false);
        app.Server.SetOn(true);
        await WhenChangedAsync(app.Server, state => state.Listening || state.Failure is not null);

        Assert.True(app.Server.State.Listening, app.Server.State.Failure);
        Assert.Null(app.Server.State.Failure);
        await using var newPipe = await ConnectPipeAsync(app.PipeName);
        await using var newClient = await ClientOnAsync(newPipe);
        Assert.True((await McpHarness.Call(newClient, "get_deletion_delay")).IsError != true);
        release();
        var finished = await inProgress;
        Assert.True(finished.IsError != true, finished.Text());
        await oldClient.Completion.WaitAsync(McpHarness.Bound);
        Assert.True((await McpHarness.Call(newClient, "get_deletion_delay")).IsError != true);
        Assert.True(app.Server.State.IsOn);
        Assert.True(app.Server.State.Listening);
        Assert.Empty(app.Log.Events.At(LogEventLevel.Error));
    }

    [WpfFact]
    public async Task StopAsync_closes_the_pipe_and_logs_it_at_Information()
    {
        await using var app = new McpHarness();
        app.Server.Start();
        await WhenChangedAsync(app.Server, state => state.Listening);
        var before = app.Log.Events.Count;

        await app.Server.StopAsync().WaitAsync(McpHarness.Bound);

        Assert.False(app.Server.State.Listening);
        Assert.NotEmpty(app.Log.After(before).At(LogEventLevel.Information));
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await using var pipe = await ConnectPipeAsync(app.PipeName, timeoutMs: 1_000);
        });
    }
}
