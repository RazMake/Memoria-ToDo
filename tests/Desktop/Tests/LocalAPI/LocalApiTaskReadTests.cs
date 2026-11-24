// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>ListTasks, GetTask, and ListTags over the Tasks service (docs/architecture/local-api-contract.md).</summary>
[Unit]
public class LocalApiTaskReadTests
{
    private static readonly DateTimeOffset Created = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 3, 2, 10, 30, 0, TimeSpan.FromHours(2));
    private static readonly DateTimeOffset Resolved = new(2026, 3, 3, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListTasks_returns_the_open_tasks_in_rank_order_in_one_reply()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([
            FakeTaskStore.Item("c"), FakeTaskStore.Item("done", resolved: Resolved), FakeTaskStore.Item("a"), FakeTaskStore.Item("b")]);

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest());

        Assert.Equal(["c", "a", "b"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_returns_resolved_tasks_too_when_asked()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([FakeTaskStore.Item("c"), FakeTaskStore.Item("done", resolved: Resolved), FakeTaskStore.Item("a")]);

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest { IncludeResolved = true });

        Assert.Equal(["c", "done", "a"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_narrows_to_the_tasks_the_query_finds()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([
            FakeTaskStore.Item("t1", title: "Buy milk"),
            FakeTaskStore.Item("t2", title: "Walk the dog"),
            FakeTaskStore.Item("t3", title: "Call", description: "about the MILK order"),
            FakeTaskStore.Item("t4", title: "Old milk", resolved: Resolved)]);

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest { Query = "milk" });

        Assert.Equal(["t1", "t3"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_narrows_to_the_tag_ignoring_case()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([
            FakeTaskStore.Item("t1", tag: "work"), FakeTaskStore.Item("t2", tag: "home"), FakeTaskStore.Item("t3"), FakeTaskStore.Item("t4", tag: "Work")]);

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest { Tag = "WORK" });

        Assert.Equal(["t1", "t4"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_applies_the_query_and_the_tag_together()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([
            FakeTaskStore.Item("t1", title: "Report", tag: "work"),
            FakeTaskStore.Item("t2", title: "Report", tag: "home"),
            FakeTaskStore.Item("t3", title: "Other", tag: "work")]);

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest { Query = "report", Tag = "work" });

        Assert.Equal(["t1"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_with_include_resolved_applies_the_query_to_resolved_tasks_too()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([FakeTaskStore.Item("t1", title: "Milk"), FakeTaskStore.Item("t2", title: "Milk", resolved: Resolved)]);

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest { Query = "milk", IncludeResolved = true });

        Assert.Equal(["t1", "t2"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_returns_an_empty_list_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("a"));
        api.Store.NoRepository = true;

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest());

        Assert.Empty(reply.Tasks);
    }

    [Fact]
    public async Task GetTask_returns_every_field_of_an_open_task()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", title: "Buy milk", description: "Two litres", tag: "home", created: Created, updated: Updated));

        var task = await api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "t1" });

        Assert.Equal("t1", task.Id);
        Assert.Equal("Buy milk", task.Title);
        Assert.Equal("Two litres", task.Description);
        Assert.Equal("home", task.Tag);
        Assert.Equal(Created, task.Created.ToDateTimeOffset());
        Assert.Equal(Updated, task.Updated.ToDateTimeOffset());
        Assert.Null(task.Resolved);
        Assert.Equal("task:t1", task.Link);
    }

    [Fact]
    public async Task GetTask_leaves_the_tag_unset_for_a_task_with_no_tag()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1"));

        var task = await api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "t1" });

        Assert.False(task.HasTag);
    }

    [Fact]
    public async Task GetTask_returns_a_resolved_task_with_its_resolved_time()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", resolved: Resolved));

        var task = await api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "t1" });

        Assert.Equal(Resolved, task.Resolved.ToDateTimeOffset());
    }

    [Fact]
    public async Task ListTasks_carries_the_same_fields_as_GetTask()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", title: "Buy milk", description: "Two litres", tag: "home", created: Created, updated: Updated));

        var listed = (await api.Tasks.ListTasksAsync(new V1.ListTasksRequest())).Tasks.Single();

        Assert.Equal(await api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "t1" }), listed);
    }

    [Fact]
    public async Task GetTask_of_an_unknown_ID_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "missing" }));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task GetTask_with_no_repository_fails_with_NOT_FOUND()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("a"));
        api.Store.NoRepository = true;

        var failure = await CallOutcome.FailsAsync(api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "a" }));

        Assert.Equal(StatusCode.NotFound, failure.StatusCode);
    }

    [Fact]
    public async Task ListTags_returns_each_tag_in_use_with_its_count_in_the_order_Tasks_gives()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([
            FakeTaskStore.Item("t1", tag: "home"), FakeTaskStore.Item("t2", tag: "work"), FakeTaskStore.Item("t3", tag: "work"), FakeTaskStore.Item("t4")]);

        var reply = await api.Tasks.ListTagsAsync(new V1.ListTagsRequest());

        Assert.Equal([("work", 2), ("home", 1)], reply.Tags.Select(tag => (tag.Tag, tag.Count)));
    }

    [Fact]
    public async Task ListTags_returns_an_empty_list_when_no_repository_is_set()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("a", tag: "work"));
        api.Store.NoRepository = true;

        var reply = await api.Tasks.ListTagsAsync(new V1.ListTagsRequest());

        Assert.Empty(reply.Tags);
    }

    [Fact]
    public async Task Reading_never_touches_the_draft_or_an_open_edit()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("a", tag: "work"));

        await api.Tasks.ListTasksAsync(new V1.ListTasksRequest { Query = "title", Tag = "work" });
        await api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "a" });
        await api.Tasks.ListTagsAsync(new V1.ListTagsRequest());

        Assert.Empty(api.Store.ForbiddenCalls);
    }
}
