// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using System.IO;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;
using Grpc.Core;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>Start and stop of the Local API, against a fake host.</summary>
[Unit]
public class LocalApiLifetimeTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Start_returns_at_once_without_waiting_for_the_host_to_listen()
    {
        TaskCompletionSource bindGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var api = new ApiHarness(new FakeApiHost { StartGate = bindGate.Task });

        await Task.Run(api.Lifetime.Start).WaitAsync(Bound);

        await api.Host.StartCalledAsync();
        bindGate.SetResult();
    }

    [Fact]
    public async Task Start_logs_Information_with_the_pipe_name_once_the_host_listens()
    {
        await using var api = new ApiHarness();

        api.Lifetime.Start();

        var entry = await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);
        Assert.Equal(LogEventLevel.Information, entry.Level);
    }

    [Fact]
    public async Task A_repeated_Start_starts_the_host_once()
    {
        await using var api = new ApiHarness();

        api.Lifetime.Start();
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);

        Assert.Equal(1, api.Host.StartCount);
    }

    [Fact]
    public async Task A_host_that_cannot_listen_logs_an_Error_naming_the_pipe_and_Start_does_not_throw()
    {
        await using var api = new ApiHarness(new FakeApiHost { StartFailure = new IOException("The pipe is taken.") });

        api.Lifetime.Start();

        var entry = await api.Log.EntryAsync(LogEventLevel.Error, ApiHarness.PipeName);
        Assert.Equal(LogEventLevel.Error, entry.Level);
    }

    [Fact]
    public async Task Stop_works_after_a_failed_start()
    {
        await using var api = new ApiHarness(new FakeApiHost { StartFailure = new IOException("The pipe is taken.") });
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Error, ApiHarness.PipeName);

        await api.Lifetime.StopAsync().WaitAsync(Bound);
    }

    [Fact]
    public async Task Stop_stops_the_host_and_logs_Information_with_the_pipe_name()
    {
        await using var api = new ApiHarness();
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);

        await api.Lifetime.StopAsync().WaitAsync(Bound);

        Assert.Equal(1, api.Host.StopCount);
        Assert.Equal(2, api.Log.Events.At(LogEventLevel.Information, ApiHarness.PipeName).Count);
    }

    [Fact]
    public async Task Stop_works_when_Start_never_ran()
    {
        await using var api = new ApiHarness();

        await api.Lifetime.StopAsync().WaitAsync(Bound);

        api.Log.Events.AssertNoProblems();
    }

    [Fact]
    public async Task Stop_waits_for_the_host_to_finish_the_running_calls()
    {
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var api = new ApiHarness(new FakeApiHost { StopGate = finish.Task });
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);

        var stopping = api.Lifetime.StopAsync();
        await api.Host.StopCalledAsync();

        Assert.False(stopping.IsCompleted);

        finish.SetResult();
        await stopping.WaitAsync(Bound);
        Assert.Equal(0, api.Host.GiveUpCount);
    }

    [Fact]
    public async Task Stop_gives_up_on_running_calls_after_its_wait_and_still_completes()
    {
        await using var api = new ApiHarness(
            new FakeApiHost { StopGate = new TaskCompletionSource().Task }, stopWait: TimeSpan.FromMilliseconds(200));
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);

        await api.Lifetime.StopAsync().WaitAsync(Bound);

        Assert.Equal(1, api.Host.GiveUpCount);
    }

    [Fact]
    public async Task Repeated_Stop_calls_join_the_same_stop()
    {
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var api = new ApiHarness(new FakeApiHost { StopGate = finish.Task });
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);

        var first = api.Lifetime.StopAsync();
        var second = api.Lifetime.StopAsync();
        await api.Host.StopCalledAsync();

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);

        finish.SetResult();
        await Task.WhenAll(first, second).WaitAsync(Bound);
        Assert.Equal(1, api.Host.StopCount);
    }

    [Fact]
    public async Task A_call_made_while_the_app_is_stopping_gets_UNAVAILABLE()
    {
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var api = new ApiHarness(new FakeApiHost { StopGate = finish.Task });
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);
        var stopping = api.Lifetime.StopAsync();
        await api.Host.StopCalledAsync();

        var failure = await CallOutcome.FailsAsync(api.Tasks.ListTasksAsync(new V1.ListTasksRequest()));

        Assert.Equal(StatusCode.Unavailable, failure.StatusCode);
        finish.SetResult();
        await stopping.WaitAsync(Bound);
    }

    [Fact]
    public async Task A_call_made_after_Stop_gets_UNAVAILABLE()
    {
        await using var api = new ApiHarness();
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);
        await api.Lifetime.StopAsync().WaitAsync(Bound);

        var failure = await CallOutcome.FailsAsync(api.Tasks.ListTasksAsync(new V1.ListTasksRequest()));

        Assert.Equal(StatusCode.Unavailable, failure.StatusCode);
    }

    [Fact]
    public async Task A_call_made_while_the_app_is_running_is_answered()
    {
        await using var api = new ApiHarness();
        api.Lifetime.Start();
        await api.Log.EntryAsync(LogEventLevel.Information, ApiHarness.PipeName);

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest());

        Assert.Empty(reply.Tasks);
    }
}
