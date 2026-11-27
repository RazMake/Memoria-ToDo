// Every call here has a 30-second deadline, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Grpc.Core;
using Memoria.ToDo.Api.Client;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>The Local API over a real named pipe, reached the way a client reaches it, with the shipped .NET client: only what the in-memory tests can't show.</summary>
[Integration]
public class LocalApiPipeTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(15);

    private static string NewPipeName() => "Memoria-ToDo.Test." + Guid.NewGuid().ToString("N");

    /// <summary>A deadline that makes a call that never ends fail the test instead of hanging it.</summary>
    private static DateTime Deadline => DateTime.UtcNow.AddSeconds(30);

    private sealed class RealApi : IAsyncDisposable
    {
        private RealApi(string pipeName)
        {
            Lifetime = LocalApiServer.Create(Store, Contacts, Sync, pipeName, Log.CreateLogger());
            Lifetime.Start();
            Client = MemoriaClient.Connect(pipeName);
            Tasks = Client.Tasks;
        }

        public static async Task<RealApi> StartAsync(string pipeName)
        {
            var api = new RealApi(pipeName);
            var failures = new FailureList();
            try
            {
                var entry = await api.Log.AnyEntryAsync(pipeName);
                if (entry.Level != LogEventLevel.Information)
                    throw new InvalidOperationException($"The Local API didn't listen on the pipe {pipeName}: {entry.RenderMessage()}", entry.Exception);
            }
            catch (Exception failure)
            {
                failures.Add(failure);
                try
                {
                    await api.DisposeAsync();
                }
                catch (Exception cleanup)
                {
                    failures.Add(cleanup);
                }
            }

            failures.ThrowIfAny();
            return api;
        }

        public FakeTaskStore Store { get; } = new();

        public FakeSyncService Sync { get; } = new();

        public Memoria.ToDo.Shared.Contacts.IContacts Contacts { get; } = TestContactStore.Create(new FakeFileSystem()).Contacts;

        public SignalLogSink Log { get; } = new();

        public ILocalApiLifetime Lifetime { get; }

        public MemoriaClient Client { get; }

        public V1.Tasks.TasksClient Tasks { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Lifetime.StopAsync().WaitAsync(Bound);
        }
    }

    [Fact]
    public async Task A_client_creates_a_task_and_lists_it_over_the_pipe()
    {
        await using var api = await RealApi.StartAsync(NewPipeName());

        var request = new V1.CreateTaskRequest { Title = "Buy milk" };
        request.Tags.AddRange(["Home Page", "errands"]);
        var created = await api.Tasks.CreateTaskAsync(request, deadline: Deadline);
        var listed = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest(), deadline: Deadline);

        Assert.Equal([created.Id], listed.Tasks.Select(task => task.Id));
        Assert.Equal("Buy milk", listed.Tasks.Single().Title);
        Assert.Equal(["home-page", "errands"], listed.Tasks.Single().Tags);
        Assert.Equal("home-page", listed.Tasks.Single().Tag);
    }

    [Fact]
    public async Task The_Node_client_creates_and_lists_a_task_over_the_pipe_and_names_the_same_pipe_as_the_NET_client()
    {
        var name = NewPipeName();
        await using var api = await RealApi.StartAsync(name);
        var dist = Path.Combine(NodeClientFolder(), "dist");

        // The script runs the built package the way a program does: through connect's module, with the test's own pipe name.
        const string script = """
            const [connectUrl, pipeUrl, pipeName] = process.argv.slice(1);
            const { connectTo } = await import(connectUrl);
            const { pipeNameForCurrentUser } = await import(pipeUrl);
            const client = connectTo(pipeName);
            try {
              const created = await client.tasks.createTask({ title: 'Buy milk', tag: 'home' });
              const listed = await client.tasks.listTasks({});
              console.log(JSON.stringify({ currentUserPipe: pipeNameForCurrentUser(), createdId: created.id, listed: listed.tasks.map((t) => [t.id, t.title]) }));
            } finally {
              client.close();
            }
            """;
        var start = new System.Diagnostics.ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[]
        {
            "--input-type=module", "-e", script,
            new Uri(Path.Combine(dist, "connect.js")).AbsoluteUri, new Uri(Path.Combine(dist, "pipe.js")).AbsoluteUri, name,
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var node = System.Diagnostics.Process.Start(start)!;
        var output = node.StandardOutput.ReadToEndAsync();
        var errors = node.StandardError.ReadToEndAsync();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await node.WaitForExitAsync(bound.Token);
        }
        catch (OperationCanceledException)
        {
            node.Kill(entireProcessTree: true);
            throw;
        }

        Assert.True(node.ExitCode == 0, await errors);
        using var result = System.Text.Json.JsonDocument.Parse(await output);
        var listed = result.RootElement.GetProperty("listed").EnumerateArray().Select(row => (row[0].GetString(), row[1].GetString())).ToList();
        Assert.Equal(Memoria.ToDo.Api.Contract.LocalApiPipe.ForCurrentUser(), result.RootElement.GetProperty("currentUserPipe").GetString());
        Assert.Equal([(result.RootElement.GetProperty("createdId").GetString(), "Buy milk")], listed);
        Assert.Equal("Buy milk", (await api.Tasks.ListTasksAsync(new V1.ListTasksRequest(), deadline: Deadline)).Tasks.Single().Title);
    }

    private static string NodeClientFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Memoria.ToDo.slnx")))
            {
                return Path.Combine(folder.FullName, "src", "Api", "NodeClient");
            }
        }

        throw new InvalidOperationException("Memoria.ToDo.slnx wasn't found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public async Task A_client_watching_tasks_over_the_pipe_gets_a_notice_at_once_and_another_after_a_change()
    {
        await using var api = await RealApi.StartAsync(NewPipeName());
        using var stream = api.Tasks.WatchTasks(new V1.WatchTasksRequest(), deadline: Deadline);
        using var bound = new CancellationTokenSource(Bound);

        Assert.True(await stream.ResponseStream.MoveNext(bound.Token));
        api.Store.RaiseChanged();

        Assert.True(await stream.ResponseStream.MoveNext(bound.Token));
    }

    [Fact]
    public async Task The_pipe_denies_network_logons_and_lets_only_the_current_user_in()
    {
        var name = NewPipeName();
        await using var api = await RealApi.StartAsync(name);
        await api.Tasks.ListTasksAsync(new V1.ListTasksRequest(), deadline: Deadline);

        // Connect once more by hand and read the pipe's access list.
        await using var probe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await probe.ConnectAsync(10_000);
        var rules = probe.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().ToList();
        var me = WindowsIdentity.GetCurrent().User!;
        var network = new SecurityIdentifier(WellKnownSidType.NetworkSid, null);

        Assert.Contains(rules, rule => rule.AccessControlType == AccessControlType.Deny && rule.IdentityReference.Equals(network));
        Assert.Contains(rules, rule => rule.AccessControlType == AccessControlType.Allow && rule.IdentityReference.Equals(me));
        Assert.All(rules.Where(rule => rule.AccessControlType == AccessControlType.Allow), rule => Assert.Equal(me, rule.IdentityReference));
    }

    [Fact]
    public async Task After_Stop_a_call_gets_UNAVAILABLE_and_the_pipe_name_is_free_again()
    {
        var name = NewPipeName();
        await using var api = await RealApi.StartAsync(name);
        await api.Tasks.ListTasksAsync(new V1.ListTasksRequest(), deadline: Deadline);

        await api.Lifetime.StopAsync().WaitAsync(Bound);

        var failure = await CallOutcome.FailsAsync(api.Tasks.ListTasksAsync(new V1.ListTasksRequest(), deadline: Deadline));
        var failures = new FailureList();
        failures.Attempt(() => CallOutcome.AssertStatus(failure, StatusCode.Unavailable, api.Log));
        failures.Attempt(() =>
        {
            using var reused = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance);
        });
        failures.ThrowIfAny();
    }

    [Fact]
    public async Task When_another_process_holds_the_pipe_name_the_API_logs_an_Error_naming_it_and_the_app_keeps_running()
    {
        var name = NewPipeName();
        await using var holder = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance);
        var store = new FakeTaskStore();
        SignalLogSink log = new();
        var lifetime = LocalApiServer.Create(store, TestContactStore.Create(new FakeFileSystem()).Contacts, new FakeSyncService(), name, log.CreateLogger());

        lifetime.Start();

        var entry = await log.EntryAsync(LogEventLevel.Error, name);
        Assert.Equal(LogEventLevel.Error, entry.Level);
        await lifetime.StopAsync().WaitAsync(Bound);
    }

    [Fact]
    public async Task Starting_the_API_on_a_pipe_name_another_process_holds_fails_with_the_logged_error()
    {
        var name = NewPipeName();
        await using var holder = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var api = await RealApi.StartAsync(name);
        });

        Assert.Contains(name, failure.Message);
    }

    [Fact]
    public async Task Stop_gives_up_on_a_call_that_never_ends_after_about_5_seconds()
    {
        await using var api = await RealApi.StartAsync(NewPipeName());
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ISyncStatus> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Sync.SyncOperation = _ =>
        {
            entered.TrySetResult();
            return release.Task;
        };
        using var call = api.Client.Sync.SyncNowAsync(new V1.SyncNowRequest(), deadline: Deadline);
        Task running = call.ResponseAsync;
        var failures = new FailureList();
        try
        {
            var first = await Task.WhenAny(entered.Task, running).WaitAsync(Bound);
            await first;
            Assert.True(entered.Task.IsCompleted);
            Assert.False(running.IsCompleted);

            var started = System.Diagnostics.Stopwatch.StartNew();
            await api.Lifetime.StopAsync().WaitAsync(TimeSpan.FromSeconds(20));
            started.Stop();

            Assert.InRange(started.Elapsed, TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(12));
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
        finally
        {
            release.TrySetResult(api.Sync.Status);
        }

        try
        {
            await CallOutcome.FailsAsync(running);
        }
        catch (Exception cleanup)
        {
            failures.Add(cleanup);
        }

        failures.ThrowIfAny();
    }
}
