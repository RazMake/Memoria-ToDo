// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using System.IO;
using Grpc.Core;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>How a failure in Tasks becomes a gRPC status, and what each call logs (docs/architecture/local-api-contract.md, logging.md).</summary>
[Unit]
public class LocalApiErrorAndLogTests
{
    private static readonly DateTimeOffset Base = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    public static readonly TheoryData<Exception, StatusCode> Failures = new()
    {
        { new TaskNotFoundException(), StatusCode.NotFound },
        { new ArgumentException("bad"), StatusCode.InvalidArgument },
        { new ArgumentOutOfRangeException("x"), StatusCode.InvalidArgument },
        { new InvalidDataException("not an image"), StatusCode.InvalidArgument },
        { new ImageTooLargeException(), StatusCode.InvalidArgument },
        { new NoRepositoryFolderException(), StatusCode.FailedPrecondition },
        { new SkippedFileException(new FakeSkippedFile("tags.json", "Not JSON.")), StatusCode.FailedPrecondition },
        { FakeTaskStore.StaleOf(FakeTaskStore.Item("t1")), StatusCode.Aborted },
        { new InvalidOperationException("boom"), StatusCode.Internal },
        { new IOException("disk"), StatusCode.Internal },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task A_failure_in_Tasks_ends_the_call_with_its_mapped_status(Exception failure, StatusCode expected)
    {
        await using var api = new ApiHarness();
        api.Store.Failure = failure;

        var outcome = await CallOutcome.FailsAsync(api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "x" }));

        Assert.Equal(expected, outcome.StatusCode);
    }

    [Fact]
    public async Task The_status_detail_is_the_message_of_the_failure()
    {
        await using var api = new ApiHarness();
        api.Store.Failure = new ArgumentException("The title can't be empty.");

        var outcome = await CallOutcome.FailsAsync(api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "" }));

        Assert.Contains("The title can't be empty.", outcome.Status.Detail);
    }

    [Fact]
    public async Task An_unmapped_failure_is_logged_as_an_Error_with_its_exception()
    {
        await using var api = new ApiHarness();
        var failure = new InvalidOperationException("boom");
        api.Store.Failure = failure;
        await CallOutcome.FailsAsync(api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "x" }));

        var entry = await api.Log.EntryAsync(LogEventLevel.Error, "CreateTask");

        Assert.Same(failure, entry.Exception);
    }

    [Fact]
    public async Task A_call_that_succeeds_logs_one_Information_entry_with_the_method_and_the_status()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1"));

        await api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "t1" });

        var entry = await api.Log.EntryAsync(LogEventLevel.Information, "GetTask", "t1");
        Assert.True(entry.NamesStatus(StatusCode.OK));
        Assert.Single(api.Log.Events, e => e.EverythingIn().Contains("GetTask", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_call_the_program_caused_to_fail_logs_a_Warning_with_the_method_the_ID_and_the_status()
    {
        await using var api = new ApiHarness();
        await CallOutcome.FailsAsync(api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "missing" }));

        var entry = await api.Log.EntryAsync(LogEventLevel.Warning, "GetTask", "missing");

        Assert.True(entry.NamesStatus(StatusCode.NotFound));
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task A_stale_update_logs_a_Warning_naming_ABORTED()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", created: Base, updated: Base));
        await CallOutcome.FailsAsync(api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest
        {
            Id = "t1", BaseUpdated = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(Base.AddHours(-1)), Title = "x",
        }));

        var entry = await api.Log.EntryAsync(LogEventLevel.Warning, "UpdateTask", "t1");

        Assert.True(entry.NamesStatus(StatusCode.Aborted));
    }

    [Fact]
    public async Task A_cancelled_call_ends_with_CANCELLED_and_is_logged_once_at_Information()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1"));
        using var cancel = new CancellationTokenSource();
        api.Store.OnCall = () => cancel.Token.ThrowIfCancellationRequested();
        await cancel.CancelAsync();

        var outcome = await CallOutcome.FailsAsync(api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "t1" }, cancel.Token));

        Assert.Equal(StatusCode.Cancelled, outcome.StatusCode);
        var entry = await api.Log.EntryAsync(LogEventLevel.Information, "MoveTask");
        Assert.True(entry.NamesStatus(StatusCode.Cancelled));
        Assert.Single(api.Log.Events, e => e.Names("MoveTask"));
    }
}
