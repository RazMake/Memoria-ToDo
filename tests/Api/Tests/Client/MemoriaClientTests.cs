using System.IO.Pipes;
using Grpc.Core;
using Memoria.ToDo.Api.Contract;
using Memoria.ToDo.Testing;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Api.Client.Tests;

/// <summary>How the .NET client finds and reaches the app: the pipe it names, the options it connects with, and what a failed connect looks like to a program.</summary>
[Unit]
public class MemoriaClientTests
{
    private static async Task CallAsync(MemoriaClient client) =>
        await client.Tasks.ListTasksAsync(new V1.ListTasksRequest(), cancellationToken: TestContext.Current.CancellationToken);

    private static async Task<RpcException> FailureOfAsync(FakePipes pipes)
    {
        using var client = MemoriaClient.Connect(pipes.Factory);
        return await Assert.ThrowsAsync<RpcException>(() => CallAsync(client));
    }

    [Fact]
    public void Connect_opens_no_pipe_until_the_first_call()
    {
        var pipes = new FakePipes();

        using var client = MemoriaClient.Connect(pipes.Factory);

        Assert.Empty(pipes.Pipes);
    }

    [Fact]
    public async Task The_first_call_connects_to_the_pipe_of_the_current_user()
    {
        var pipes = new FakePipes { ConnectFailure = new TimeoutException() };

        await FailureOfAsync(pipes);

        Assert.Equal(LocalApiPipe.ForCurrentUser(), Assert.Single(pipes.Pipes).PipeName);
    }

    [Fact]
    public async Task The_pipe_is_opened_for_the_current_user_only_so_another_accounts_pipe_is_refused()
    {
        var pipes = new FakePipes { ConnectFailure = new TimeoutException() };

        await FailureOfAsync(pipes);

        Assert.True(Assert.Single(pipes.Pipes).Options.HasFlag(PipeOptions.CurrentUserOnly));
    }

    [Fact]
    public async Task The_pipe_is_opened_for_asynchronous_use_as_gRPC_needs()
    {
        var pipes = new FakePipes { ConnectFailure = new TimeoutException() };

        await FailureOfAsync(pipes);

        Assert.True(Assert.Single(pipes.Pipes).Options.HasFlag(PipeOptions.Asynchronous));
    }

    [Fact]
    public async Task The_connect_waits_at_most_one_second()
    {
        var pipes = new FakePipes { ConnectFailure = new TimeoutException() };

        await FailureOfAsync(pipes);

        Assert.Equal(1000, Assert.Single(pipes.Pipes).ConnectTimeoutMilliseconds);
    }

    [Fact]
    public async Task A_call_fails_with_UNAVAILABLE_when_no_server_listens_in_time()
    {
        var failure = await FailureOfAsync(new FakePipes { ConnectFailure = new TimeoutException() });

        Assert.Equal(StatusCode.Unavailable, failure.StatusCode);
    }

    [Fact]
    public async Task A_call_fails_with_UNAVAILABLE_when_another_account_owns_the_pipe()
    {
        var failure = await FailureOfAsync(new FakePipes { ConnectFailure = new UnauthorizedAccessException() });

        Assert.Equal(StatusCode.Unavailable, failure.StatusCode);
    }

    [Fact]
    public async Task A_pipe_whose_connect_failed_is_closed()
    {
        var pipes = new FakePipes { ConnectFailure = new TimeoutException() };

        await FailureOfAsync(pipes);

        Assert.True(Assert.Single(pipes.Pipes).Disposed);
    }

    [Fact]
    public void It_exposes_the_four_service_clients()
    {
        using var client = MemoriaClient.Connect(new FakePipes().Factory);

        Assert.IsType<V1.Tasks.TasksClient>(client.Tasks);
        Assert.IsType<V1.Contacts.ContactsClient>(client.Contacts);
        Assert.IsType<V1.Sync.SyncClient>(client.Sync);
        Assert.IsType<V1.Settings.SettingsClient>(client.Settings);
    }

    [Fact]
    public async Task A_call_after_Dispose_is_refused_and_opens_no_pipe()
    {
        var pipes = new FakePipes();
        var client = MemoriaClient.Connect(pipes.Factory);

        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => CallAsync(client));
        Assert.Empty(pipes.Pipes);
    }

    [Fact]
    public void Dispose_can_be_called_twice()
    {
        var client = MemoriaClient.Connect(new FakePipes().Factory);
        client.Dispose();

        client.Dispose();
    }
}
