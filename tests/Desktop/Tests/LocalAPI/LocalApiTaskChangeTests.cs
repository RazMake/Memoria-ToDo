// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using System.IO;
using Grpc.Core;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>CreateTask, UpdateTask, AddImage, DeleteTask, MoveTask, ResolveTask, and ReopenTask (docs/architecture/local-api-contract.md).</summary>
[Unit]
public class LocalApiTaskChangeTests
{
    private static readonly DateTimeOffset Base = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private static ApiHarness WithTask(out TaskItem task, string id = "t1")
    {
        var api = new ApiHarness();
        task = FakeTaskStore.Item(id, title: "Old", description: "Old text", tag: "work", created: Base, updated: Base);
        api.Store.Tasks.Add(task);
        return api;
    }

    [Fact]
    public async Task CreateTask_adds_the_task_through_the_direct_changes_and_returns_it()
    {
        await using var api = new ApiHarness();

        var created = await api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "Buy milk", Description = "Two litres", Tag = "home" });

        Assert.Equal([("Buy milk", "Two litres", (string?)"home")], api.Store.DirectChanges.Adds);
        Assert.Equal("Buy milk", created.Title);
        Assert.Equal(api.Store.Tasks.Single().Id, created.Id);
        Assert.Empty(api.Store.ForbiddenCalls);
    }

    [Fact]
    public async Task CreateTask_without_a_tag_adds_a_task_with_no_tag()
    {
        await using var api = new ApiHarness();

        var created = await api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "Buy milk" });

        Assert.Equal([("Buy milk", "", (string?)null)], api.Store.DirectChanges.Adds);
        Assert.False(created.HasTag);
    }

    [Fact]
    public async Task CreateTask_with_no_repository_fails_with_FAILED_PRECONDITION()
    {
        await using var api = new ApiHarness();
        api.Store.NoRepository = true;

        var failure = await CallOutcome.FailsAsync(api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "x" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task UpdateTask_passes_the_base_time_and_every_set_field_to_the_direct_changes()
    {
        await using var api = WithTask(out _);

        var updated = await api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest
        {
            Id = "t1", BaseUpdated = Timestamp.FromDateTimeOffset(Base), Title = "New", Description = "New text", Tag = "home",
        });

        Assert.Equal([("t1", Base, (string?)"New", (string?)"New text", (string?)"home")], api.Store.DirectChanges.Updates);
        Assert.Equal("New", updated.Title);
        Assert.Equal("New text", updated.Description);
        Assert.Equal("home", updated.Tag);
        Assert.Empty(api.Store.ForbiddenCalls);
    }

    [Fact]
    public async Task UpdateTask_passes_unset_fields_as_unchanged()
    {
        await using var api = WithTask(out _);

        var updated = await api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest { Id = "t1", BaseUpdated = Timestamp.FromDateTimeOffset(Base), Title = "New" });

        Assert.Equal([("t1", Base, (string?)"New", (string?)null, (string?)null)], api.Store.DirectChanges.Updates);
        Assert.Equal("Old text", updated.Description);
        Assert.Equal("work", updated.Tag);
    }

    [Fact]
    public async Task UpdateTask_with_an_empty_tag_removes_the_tag()
    {
        await using var api = WithTask(out _);

        var updated = await api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest { Id = "t1", BaseUpdated = Timestamp.FromDateTimeOffset(Base), Tag = "" });

        Assert.Equal([("t1", Base, (string?)null, (string?)null, (string?)"")], api.Store.DirectChanges.Updates);
        Assert.False(updated.HasTag);
    }

    [Fact]
    public async Task UpdateTask_with_an_empty_description_clears_it_rather_than_leaving_it()
    {
        await using var api = WithTask(out _);

        var updated = await api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest { Id = "t1", BaseUpdated = Timestamp.FromDateTimeOffset(Base), Description = "" });

        Assert.Equal([("t1", Base, (string?)null, (string?)"", (string?)null)], api.Store.DirectChanges.Updates);
        Assert.Equal("", updated.Description);
    }

    [Fact]
    public async Task UpdateTask_without_a_base_time_fails_with_INVALID_ARGUMENT_and_changes_nothing()
    {
        await using var api = WithTask(out _);

        var failure = await CallOutcome.FailsAsync(api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest { Id = "t1", Title = "New" }));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.DirectChanges.Updates);
    }

    [Fact]
    public async Task UpdateTask_of_a_task_changed_since_the_base_time_fails_with_ABORTED()
    {
        await using var api = WithTask(out _);

        var failure = await CallOutcome.FailsAsync(api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest
        {
            Id = "t1", BaseUpdated = Timestamp.FromDateTimeOffset(Base.AddMinutes(-5)), Title = "New",
        }));

        Assert.Equal(StatusCode.Aborted, failure.StatusCode);
        Assert.Equal("Old", api.Store.Tasks.Single().Title);
    }

    [Fact]
    public async Task UpdateTask_of_an_unknown_ID_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest
        {
            Id = "missing", BaseUpdated = Timestamp.FromDateTimeOffset(Base), Title = "New",
        }));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task AddImage_passes_the_bytes_and_alt_text_to_the_direct_changes()
    {
        await using var api = WithTask(out _);
        byte[] image = [1, 2, 3, 4];

        var changed = await api.Tasks.AddImageAsync(new V1.AddImageRequest { Id = "t1", Image = ByteString.CopyFrom(image), AltText = "A chart" });

        var call = Assert.Single(api.Store.DirectChanges.Images);
        Assert.Equal("t1", call.Id);
        Assert.Equal(image, call.Image);
        Assert.Equal("A chart", call.AltText);
        Assert.Equal("t1", changed.Id);
        Assert.Contains("A chart", changed.Description);
        Assert.Empty(api.Store.ForbiddenCalls);
    }

    [Fact]
    public async Task AddImage_that_is_not_an_image_fails_with_INVALID_ARGUMENT()
    {
        await using var api = WithTask(out _);
        api.Store.Failure = new InvalidDataException("This isn't an image.");

        var failure = await CallOutcome.FailsAsync(api.Tasks.AddImageAsync(new V1.AddImageRequest { Id = "t1", Image = ByteString.CopyFrom(1) }));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task AddImage_that_is_too_large_for_Tasks_fails_with_INVALID_ARGUMENT()
    {
        await using var api = WithTask(out _);
        api.Store.Failure = new ImageTooLargeException();

        var failure = await CallOutcome.FailsAsync(api.Tasks.AddImageAsync(new V1.AddImageRequest { Id = "t1", Image = ByteString.CopyFrom(1) }));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task AddImage_accepts_an_image_of_20_MiB()
    {
        await using var api = WithTask(out _);

        await api.Tasks.AddImageAsync(new V1.AddImageRequest { Id = "t1", Image = ByteString.CopyFrom(new byte[20 * 1024 * 1024]) });

        Assert.Equal(20 * 1024 * 1024, Assert.Single(api.Store.DirectChanges.Images).Image.Length);
    }

    [Fact]
    public async Task AddImage_over_32_MiB_fails_with_RESOURCE_EXHAUSTED_before_it_reaches_Tasks()
    {
        await using var api = WithTask(out _);

        var failure = await CallOutcome.FailsAsync(api.Tasks.AddImageAsync(new V1.AddImageRequest
        {
            Id = "t1", Image = ByteString.CopyFrom(new byte[40 * 1024 * 1024]),
        }));

        Assert.Equal(StatusCode.ResourceExhausted, failure.StatusCode);
        Assert.Empty(api.Store.DirectChanges.Images);
    }

    [Fact]
    public async Task DeleteTask_deletes_through_Tasks_without_touching_an_edit()
    {
        await using var api = WithTask(out _);

        await api.Tasks.DeleteTaskAsync(new V1.DeleteTaskRequest { Id = "t1" });

        Assert.Empty(api.Store.Tasks);
        Assert.Contains("Delete t1", api.Store.Calls);
        Assert.Empty(api.Store.ForbiddenCalls);
    }

    [Fact]
    public async Task DeleteTask_of_an_unknown_ID_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.Tasks.DeleteTaskAsync(new V1.DeleteTaskRequest { Id = "missing" }));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task DeleteTask_with_no_repository_fails_with_FAILED_PRECONDITION()
    {
        await using var api = new ApiHarness();
        api.Store.NoRepository = true;

        var failure = await CallOutcome.FailsAsync(api.Tasks.DeleteTaskAsync(new V1.DeleteTaskRequest { Id = "t1" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task MoveTask_passes_the_task_and_both_neighbors()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([FakeTaskStore.Item("a"), FakeTaskStore.Item("b"), FakeTaskStore.Item("c")]);

        var moved = await api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "c", PreviousId = "a", NextId = "b" });

        Assert.Contains("Move c a b", api.Store.Calls);
        Assert.Equal("c", moved.Id);
    }

    [Fact]
    public async Task MoveTask_passes_a_missing_neighbor_as_none()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([FakeTaskStore.Item("a"), FakeTaskStore.Item("b")]);

        await api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "b", NextId = "a" });
        await api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "a", PreviousId = "b" });

        Assert.Equal(["Move b - a", "Move a b -"], api.Store.Calls);
    }

    [Fact]
    public async Task MoveTask_of_an_unknown_ID_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "missing" }));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task MoveTask_that_Tasks_rejects_as_an_invalid_argument_fails_with_INVALID_ARGUMENT()
    {
        await using var api = WithTask(out _);
        api.Store.Failure = new ArgumentException("The neighbors aren't next to each other.");

        var failure = await CallOutcome.FailsAsync(api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "t1", PreviousId = "x", NextId = "y" }));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task MoveTask_with_no_repository_fails_with_FAILED_PRECONDITION()
    {
        await using var api = new ApiHarness();
        api.Store.NoRepository = true;

        var failure = await CallOutcome.FailsAsync(api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "t1" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task ResolveTask_resolves_the_task_and_returns_it_with_its_resolved_time()
    {
        await using var api = WithTask(out _);

        var resolved = await api.Tasks.ResolveTaskAsync(new V1.ResolveTaskRequest { Id = "t1" });

        Assert.Contains("Resolve t1", api.Store.Calls);
        Assert.NotNull(resolved.Resolved);
    }

    [Fact]
    public async Task ResolveTask_of_an_unknown_ID_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.Tasks.ResolveTaskAsync(new V1.ResolveTaskRequest { Id = "missing" }));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task ResolveTask_with_no_repository_fails_with_FAILED_PRECONDITION()
    {
        await using var api = new ApiHarness();
        api.Store.NoRepository = true;

        var failure = await CallOutcome.FailsAsync(api.Tasks.ResolveTaskAsync(new V1.ResolveTaskRequest { Id = "t1" }));

        Assert.Equal(StatusCode.FailedPrecondition, failure.StatusCode);
    }

    [Fact]
    public async Task ReopenTask_reopens_a_resolved_task_and_returns_it_without_a_resolved_time()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", resolved: Base));

        var reopened = await api.Tasks.ReopenTaskAsync(new V1.ReopenTaskRequest { Id = "t1" });

        Assert.Contains("Reopen t1", api.Store.Calls);
        Assert.Null(reopened.Resolved);
    }

    [Fact]
    public async Task ReopenTask_of_an_unknown_ID_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.Tasks.ReopenTaskAsync(new V1.ReopenTaskRequest { Id = "missing" }));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task Move_resolve_and_reopen_never_use_the_direct_changes()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([FakeTaskStore.Item("a"), FakeTaskStore.Item("b")]);

        await api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "a", NextId = "b" });
        await api.Tasks.ResolveTaskAsync(new V1.ResolveTaskRequest { Id = "a" });
        await api.Tasks.ReopenTaskAsync(new V1.ReopenTaskRequest { Id = "a" });

        Assert.DoesNotContain(api.Store.Calls, call => call.StartsWith("Direct.", StringComparison.Ordinal));
    }
}
