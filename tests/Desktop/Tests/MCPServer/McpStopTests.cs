using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.MCPServer.Tests;

/// <summary>Stopping the server while assistants are connected: calls in progress are waited for, and a call that comes after is refused.</summary>
[Unit]
public class McpStopTests
{
    /// <summary>Makes the next task change block inside Tasks until released, which keeps the call that made it in progress and un-cancellable.</summary>
    private sealed class Blocker
    {
        private readonly ManualResetEventSlim entered = new();
        private readonly ManualResetEventSlim release = new();

        public Blocker(McpHarness app) => app.Tasks.Changed += (_, _) =>
        {
            entered.Set();
            release.Wait(McpHarness.Bound);
        };

        public Task WhenEnteredAsync() => Task.Run(() => Assert.True(entered.Wait(McpHarness.Bound), "The call never reached Tasks."));

        public void Release() => release.Set();
    }

    [WpfFact]
    public async Task A_call_that_starts_after_stopping_began_gets_an_app_is_closing_error()
    {
        await using var app = new McpHarness();
        app.StartOff();
        var task = app.AddTask("Held");
        var other = app.AddTask("Other");
        var client = await app.ConnectAsync();
        var blocker = new Blocker(app);
        var inProgress = McpHarness.Call(client, "resolve_task", new { id = task.Id });
        await blocker.WhenEnteredAsync();
        var stopping = app.Server.StopAsync();

        var late = await McpHarness.Call(client, "get_task", new { id = other.Id });

        Assert.True(late.IsError);
        Assert.Contains("closing", late.Text(), StringComparison.OrdinalIgnoreCase);
        blocker.Release();
        await inProgress;
        await stopping.WaitAsync(McpHarness.Bound);
    }

    [WpfFact]
    public async Task A_call_that_starts_after_the_switch_turns_off_is_refused_while_a_call_in_progress_finishes()
    {
        await using var app = new McpHarness();
        app.StartOff();
        var task = app.AddTask("Keep me");
        var client = await app.ConnectAsync();
        var release = app.Logs.Hold();
        var inProgress = McpHarness.Call(client, "read_log");
        try
        {
            await app.Logs.WhenFirstReadStarted().WaitAsync(McpHarness.Bound);

            app.Server.SetOn(false);
            var late = await McpHarness.Call(client, "delete_task", new { id = task.Id });

            Assert.True(late.IsError);
            Assert.Contains(app.Tasks.All, item => item.Id == task.Id);
        }
        finally
        {
            release();
        }

        var finished = await inProgress;
        Assert.True(finished.IsError != true, finished.Text());
        await client.Completion.WaitAsync(McpHarness.Bound);
        Assert.Empty(app.Log.Events.At(LogEventLevel.Error));
    }

    [WpfFact]
    public async Task StopAsync_waits_for_a_call_in_progress_and_lets_it_finish()
    {
        await using var app = new McpHarness();
        app.StartOff();
        var task = app.AddTask("Held");
        var client = await app.ConnectAsync();
        var blocker = new Blocker(app);
        var inProgress = McpHarness.Call(client, "resolve_task", new { id = task.Id });
        await blocker.WhenEnteredAsync();

        var stopping = app.Server.StopAsync();

        Assert.False(stopping.IsCompleted);
        blocker.Release();
        var result = await inProgress;
        await stopping.WaitAsync(McpHarness.Bound);
        Assert.NotNull(app.Tasks.All.Single().Resolved);
        Assert.True(result.IsError != true, result.Text());
    }

    [WpfFact]
    public async Task StopAsync_cancels_a_log_read_in_progress_and_completes()
    {
        await using var app = new McpHarness();
        app.StartOff();
        var client = await app.ConnectAsync();
        var release = app.Logs.Hold();
        var reading = McpHarness.Call(client, "read_log");
        await app.Logs.WhenFirstReadStarted().WaitAsync(McpHarness.Bound);

        await app.Server.StopAsync().WaitAsync(McpHarness.Bound);

        await app.Logs.WhenReadsFinished(1).WaitAsync(McpHarness.Bound);
        release();
        try
        {
            await reading;
        }
        catch (ModelContextProtocol.McpException)
        {
            // A cancelled call may reach the client as a protocol error; either way it ended.
        }
    }

    [WpfFact]
    public async Task StopAsync_called_twice_completes_both_times()
    {
        await using var app = new McpHarness();
        app.StartOff();

        var first = app.Server.StopAsync();
        var second = app.Server.StopAsync();

        await Task.WhenAll(first, second).WaitAsync(McpHarness.Bound);
    }

    [WpfFact]
    public async Task StopAsync_ends_the_open_connections()
    {
        await using var app = new McpHarness();
        app.StartOff();
        var client = await app.ConnectAsync();

        await app.Server.StopAsync().WaitAsync(McpHarness.Bound);

        await client.Completion.WaitAsync(McpHarness.Bound);
    }

    [WpfFact]
    public async Task A_call_refused_because_the_app_is_closing_is_not_logged_as_a_failure()
    {
        await using var app = new McpHarness();
        app.StartOff();
        var task = app.AddTask("Held");
        var client = await app.ConnectAsync();
        var blocker = new Blocker(app);
        var inProgress = McpHarness.Call(client, "resolve_task", new { id = task.Id });
        await blocker.WhenEnteredAsync();
        var stopping = app.Server.StopAsync();

        await McpHarness.Call(client, "list_tasks");
        blocker.Release();
        await inProgress;
        await stopping.WaitAsync(McpHarness.Bound);

        Assert.Empty(app.Log.Events.At(LogEventLevel.Error));
    }

    [WpfFact]
    public async Task Two_servers_answering_the_same_tool_each_use_their_own_tasks_and_log()
    {
        await using var first = new McpHarness();
        await using var second = new McpHarness();
        first.StartOff();
        second.StartOff();

        var onFirst = await first.OkAsync("add_task", new { title = "Only on the first" });
        var onSecond = await second.OkAsync("add_task", new { title = "Only on the second" });
        var firstTasks = await first.OkAsync("list_tasks");
        var secondTasks = await second.OkAsync("list_tasks");

        Assert.Equal([onFirst.Str("id")], firstTasks.Items("tasks").Select(task => task.Str("id")));
        Assert.Equal([onSecond.Str("id")], secondTasks.Items("tasks").Select(task => task.Str("id")));
        Assert.Equal(["Only on the first"], first.Tasks.All.Select(task => task.Title));
        Assert.Equal(["Only on the second"], second.Tasks.All.Select(task => task.Title));
        first.Log.Events.SingleAt(LogEventLevel.Information, "add_task", onFirst.Str("id")!);
        second.Log.Events.SingleAt(LogEventLevel.Information, "add_task", onSecond.Str("id")!);
        Assert.DoesNotContain(first.Log.Events.Where(entry => entry.Level >= LogEventLevel.Information),
            entry => entry.EverythingIn().Contains(onSecond.Str("id")!, StringComparison.Ordinal));
        Assert.DoesNotContain(second.Log.Events.Where(entry => entry.Level >= LogEventLevel.Information),
            entry => entry.EverythingIn().Contains(onFirst.Str("id")!, StringComparison.Ordinal));
    }

    [WpfFact]
    public async Task A_second_server_keeps_using_its_own_tasks_and_log_after_the_first_server_stopped()
    {
        await using var first = new McpHarness();
        await using var second = new McpHarness();
        first.StartOff();
        second.StartOff();
        var onFirst = await first.OkAsync("add_task", new { title = "Before stopping" });
        await first.Server.StopAsync().WaitAsync(McpHarness.Bound);
        var firstEntries = first.Log.Events.Count(entry => entry.Level >= LogEventLevel.Information);

        var onSecond = await second.OkAsync("add_task", new { title = "After the other stopped" });
        var secondTasks = await second.OkAsync("list_tasks");

        Assert.Equal([onSecond.Str("id")], secondTasks.Items("tasks").Select(task => task.Str("id")));
        Assert.Equal([onFirst.Str("id")], first.Tasks.All.Select(task => task.Id));
        Assert.Equal(firstEntries, first.Log.Events.Count(entry => entry.Level >= LogEventLevel.Information));
        second.Log.Events.SingleAt(LogEventLevel.Information, "add_task", onSecond.Str("id")!);
        Assert.Empty(second.Log.Events.At(LogEventLevel.Error));
        Assert.Empty(second.Log.Events.At(LogEventLevel.Warning));
    }
}
