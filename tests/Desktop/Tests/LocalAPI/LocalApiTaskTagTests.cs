// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>How a task's tags travel as a list beside the older one-tag field (docs/architecture/local-api-contract.md).</summary>
[Unit]
public class LocalApiTaskTagTests
{
    private static readonly DateTimeOffset Base = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private static ApiHarness WithTask(params string[] tags)
    {
        var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", title: "Old", description: "Old text", tags: tags, created: Base, updated: Base));
        return api;
    }

    private static V1.UpdateTaskRequest Update() => new() { Id = "t1", BaseUpdated = Timestamp.FromDateTimeOffset(Base) };

    private static V1.TagNames Names(params string[] names)
    {
        var list = new V1.TagNames();
        list.Names.AddRange(names);
        return list;
    }

    private static string[] Numbered(int count) => [.. Enumerable.Range(1, count).Select(number => "tag" + number)];

    // Reading: behavior 31.

    [Fact]
    public async Task GetTask_returns_every_tag_in_the_tasks_order_and_the_first_as_tag()
    {
        await using var api = WithTask("work", "home", "errands");

        var task = await api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "t1" });

        Assert.Equal(["work", "home", "errands"], task.Tags);
        Assert.Equal("work", task.Tag);
    }

    [Fact]
    public async Task GetTask_of_a_task_with_no_tags_returns_an_empty_list_and_no_tag()
    {
        await using var api = WithTask();

        var task = await api.Tasks.GetTaskAsync(new V1.GetTaskRequest { Id = "t1" });

        Assert.Empty(task.Tags);
        Assert.False(task.HasTag);
    }

    [Fact]
    public async Task ListTasks_carries_each_tasks_tags_in_its_order()
    {
        await using var api = WithTask("b", "a");

        var listed = (await api.Tasks.ListTasksAsync(new V1.ListTasksRequest())).Tasks.Single();

        Assert.Equal(["b", "a"], listed.Tags);
        Assert.Equal("b", listed.Tag);
    }

    // Listing: behavior 33.

    [Fact]
    public async Task ListTasks_with_tags_keeps_only_the_tasks_that_have_every_one()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([
            FakeTaskStore.Item("both", tags: ["home", "work"]),
            FakeTaskStore.Item("reversed", tags: ["work", "x", "home"]),
            FakeTaskStore.Item("only-work", tags: ["work"]),
            FakeTaskStore.Item("only-home", tags: ["home"]),
            FakeTaskStore.Item("none")]);

        var request = new V1.ListTasksRequest();
        request.Tags.AddRange(["home", "work"]);
        var reply = await api.Tasks.ListTasksAsync(request);

        Assert.Equal(["both", "reversed"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_with_tags_applies_the_query_too()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([
            FakeTaskStore.Item("t1", title: "Report", tags: ["home", "work"]),
            FakeTaskStore.Item("t2", title: "Other", tags: ["home", "work"]),
            FakeTaskStore.Item("t3", title: "Report", tags: ["home"])]);

        var request = new V1.ListTasksRequest { Query = "report" };
        request.Tags.AddRange(["home", "work"]);
        var reply = await api.Tasks.ListTasksAsync(request);

        Assert.Equal(["t1"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_with_tag_matches_that_tag_anywhere_in_a_tasks_list()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([
            FakeTaskStore.Item("first", tags: ["work", "home"]),
            FakeTaskStore.Item("last", tags: ["home", "errands", "work"]),
            FakeTaskStore.Item("other", tags: ["home"])]);

        var reply = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest { Tag = "work" });

        Assert.Equal(["first", "last"], reply.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_matches_a_filter_tag_after_normalizing_it()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([FakeTaskStore.Item("t1", tags: ["my-work", "home"]), FakeTaskStore.Item("t2", tags: ["home"])]);

        var byTag = await api.Tasks.ListTasksAsync(new V1.ListTasksRequest { Tag = "  My   Work " });
        var request = new V1.ListTasksRequest();
        request.Tags.AddRange(["MY WORK", "Home"]);
        var byTags = await api.Tasks.ListTasksAsync(request);

        Assert.Equal(["t1"], byTag.Tasks.Select(task => task.Id));
        Assert.Equal(["t1"], byTags.Tasks.Select(task => task.Id));
    }

    [Fact]
    public async Task ListTasks_with_more_than_15_tags_has_no_limit()
    {
        await using var api = WithTask("a");

        var request = new V1.ListTasksRequest();
        request.Tags.AddRange(Numbered(16));
        var reply = await api.Tasks.ListTasksAsync(request);

        Assert.Empty(reply.Tasks);
    }

    // Both fields: behavior 34.

    [Fact]
    public async Task ListTasks_with_both_tag_and_tags_fails_with_INVALID_ARGUMENT()
    {
        await using var api = WithTask("work");
        var request = new V1.ListTasksRequest { Tag = "work" };
        request.Tags.Add("work");

        var failure = await CallOutcome.FailsAsync(api.Tasks.ListTasksAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
    }

    [Fact]
    public async Task CreateTask_with_both_tag_and_tags_fails_with_INVALID_ARGUMENT_and_adds_nothing()
    {
        await using var api = new ApiHarness();
        var request = new V1.CreateTaskRequest { Title = "x", Tag = "home" };
        request.Tags.Add("work");

        var failure = await CallOutcome.FailsAsync(api.Tasks.CreateTaskAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.DirectChanges.Adds);
        Assert.Empty(api.Store.Tasks);
    }

    [Fact]
    public async Task UpdateTask_with_both_tag_and_tags_fails_with_INVALID_ARGUMENT_and_changes_nothing()
    {
        await using var api = WithTask("work");
        var request = Update();
        request.Tag = "home";
        request.Tags = Names("a");

        var failure = await CallOutcome.FailsAsync(api.Tasks.UpdateTaskAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.DirectChanges.Updates);
        Assert.Equal(["work"], api.Store.Tasks.Single().Tags);
    }

    // Creating: behaviors 31 and 35 to 37, and saving in order.

    [Fact]
    public async Task CreateTask_with_tags_saves_them_in_order()
    {
        await using var api = new ApiHarness();
        var request = new V1.CreateTaskRequest { Title = "Buy milk" };
        request.Tags.AddRange(["home", "errands", "alpha"]);

        var created = await api.Tasks.CreateTaskAsync(request);

        Assert.Equal(["home", "errands", "alpha"], Assert.Single(api.Store.DirectChanges.Adds).Tags);
        Assert.Equal(["home", "errands", "alpha"], created.Tags);
        Assert.Equal("home", created.Tag);
        Assert.Equal(["home", "errands", "alpha"], api.Store.Tasks.Single().Tags);
    }

    [Fact]
    public async Task CreateTask_with_an_empty_tag_adds_a_task_with_no_tags()
    {
        await using var api = new ApiHarness();

        var created = await api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "x", Tag = "" });

        Assert.Empty(Assert.Single(api.Store.DirectChanges.Adds).Tags);
        Assert.Empty(created.Tags);
        Assert.False(created.HasTag);
    }

    [Fact]
    public async Task CreateTask_normalizes_every_tag_and_drops_repeats_without_an_error()
    {
        await using var api = new ApiHarness();
        var request = new V1.CreateTaskRequest { Title = "x" };
        request.Tags.AddRange(["  My   Work ", "Home", "MY-WORK", "home"]);

        var created = await api.Tasks.CreateTaskAsync(request);

        Assert.Equal(["my-work", "home"], created.Tags);
        Assert.Equal("my-work", created.Tag);
    }

    [Fact]
    public async Task CreateTask_normalizes_the_older_tag()
    {
        await using var api = new ApiHarness();

        var created = await api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "x", Tag = " My Work " });

        Assert.Equal(["my-work"], created.Tags);
        Assert.Equal("my-work", created.Tag);
    }

    [Fact]
    public async Task CreateTask_with_a_tag_of_only_spaces_fails_with_INVALID_ARGUMENT_and_adds_nothing()
    {
        await using var api = new ApiHarness();

        var failure = await CallOutcome.FailsAsync(api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "x", Tag = "   " }));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.Tasks);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTask_with_a_tag_in_tags_that_is_empty_once_normalized_fails_with_INVALID_ARGUMENT(string empty)
    {
        await using var api = new ApiHarness();
        var request = new V1.CreateTaskRequest { Title = "x" };
        request.Tags.AddRange(["home", empty]);

        var failure = await CallOutcome.FailsAsync(api.Tasks.CreateTaskAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.Tasks);
    }

    [Fact]
    public async Task CreateTask_with_15_tags_succeeds()
    {
        await using var api = new ApiHarness();
        var request = new V1.CreateTaskRequest { Title = "x" };
        request.Tags.AddRange(Numbered(15));

        var created = await api.Tasks.CreateTaskAsync(request);

        Assert.Equal(15, created.Tags.Count);
    }

    [Fact]
    public async Task CreateTask_with_more_than_15_tags_fails_with_INVALID_ARGUMENT_and_adds_nothing()
    {
        await using var api = new ApiHarness();
        var request = new V1.CreateTaskRequest { Title = "x" };
        request.Tags.AddRange(Numbered(16));

        var failure = await CallOutcome.FailsAsync(api.Tasks.CreateTaskAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Empty(api.Store.Tasks);
    }

    // Updating: behaviors 31, 32, 35 to 37.

    [Fact]
    public async Task UpdateTask_with_tags_replaces_the_whole_list_in_that_order()
    {
        await using var api = WithTask("a", "b", "c");
        var request = Update();
        request.Tags = Names("z", "b");

        var updated = await api.Tasks.UpdateTaskAsync(request);

        Assert.Equal(["z", "b"], Assert.Single(api.Store.DirectChanges.Updates).Tags);
        Assert.Equal(["z", "b"], updated.Tags);
        Assert.Equal("z", updated.Tag);
    }

    [Fact]
    public async Task UpdateTask_with_an_empty_list_of_tags_removes_every_tag()
    {
        await using var api = WithTask("a", "b");
        var request = Update();
        request.Tags = new V1.TagNames();

        var updated = await api.Tasks.UpdateTaskAsync(request);

        Assert.Empty(Assert.Single(api.Store.DirectChanges.Updates).Tags!);
        Assert.Empty(updated.Tags);
        Assert.False(updated.HasTag);
    }

    [Fact]
    public async Task UpdateTask_without_tag_or_tags_leaves_every_tag()
    {
        await using var api = WithTask("a", "b");
        var request = Update();
        request.Title = "New";

        var updated = await api.Tasks.UpdateTaskAsync(request);

        Assert.Null(Assert.Single(api.Store.DirectChanges.Updates).Tags);
        Assert.Equal(["a", "b"], updated.Tags);
    }

    [Fact]
    public async Task UpdateTask_with_the_older_tag_replaces_the_whole_list_with_that_one_tag()
    {
        await using var api = WithTask("a", "b", "c");
        var request = Update();
        request.Tag = "b";

        var updated = await api.Tasks.UpdateTaskAsync(request);

        Assert.Equal(["b"], Assert.Single(api.Store.DirectChanges.Updates).Tags);
        Assert.Equal(["b"], updated.Tags);
        Assert.Equal("b", updated.Tag);
    }

    [Fact]
    public async Task UpdateTask_with_an_empty_older_tag_removes_every_tag()
    {
        await using var api = WithTask("a", "b");
        var request = Update();
        request.Tag = "";

        var updated = await api.Tasks.UpdateTaskAsync(request);

        Assert.Empty(updated.Tags);
        Assert.False(updated.HasTag);
    }

    [Fact]
    public async Task UpdateTask_normalizes_every_tag_and_drops_repeats_without_an_error()
    {
        await using var api = WithTask("a");
        var request = Update();
        request.Tags = Names("  My   Work ", "MY-WORK", "Home");

        var updated = await api.Tasks.UpdateTaskAsync(request);

        Assert.Equal(["my-work", "home"], updated.Tags);
    }

    [Fact]
    public async Task UpdateTask_normalizes_the_older_tag()
    {
        await using var api = WithTask("a");
        var request = Update();
        request.Tag = " My Work ";

        var updated = await api.Tasks.UpdateTaskAsync(request);

        Assert.Equal(["my-work"], updated.Tags);
    }

    [Fact]
    public async Task UpdateTask_with_a_tag_of_only_spaces_fails_with_INVALID_ARGUMENT_and_keeps_the_tags()
    {
        await using var api = WithTask("a");
        var request = Update();
        request.Tag = "   ";

        var failure = await CallOutcome.FailsAsync(api.Tasks.UpdateTaskAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Equal(["a"], api.Store.Tasks.Single().Tags);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateTask_with_a_tag_in_tags_that_is_empty_once_normalized_fails_with_INVALID_ARGUMENT(string empty)
    {
        await using var api = WithTask("a");
        var request = Update();
        request.Tags = Names("home", empty);

        var failure = await CallOutcome.FailsAsync(api.Tasks.UpdateTaskAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Equal(["a"], api.Store.Tasks.Single().Tags);
    }

    [Fact]
    public async Task UpdateTask_with_15_tags_succeeds()
    {
        await using var api = WithTask("a");
        var request = Update();
        request.Tags = Names(Numbered(15));

        var updated = await api.Tasks.UpdateTaskAsync(request);

        Assert.Equal(15, updated.Tags.Count);
    }

    [Fact]
    public async Task UpdateTask_with_more_than_15_tags_fails_with_INVALID_ARGUMENT_and_keeps_the_tags()
    {
        await using var api = WithTask("a");
        var request = Update();
        request.Tags = Names(Numbered(16));

        var failure = await CallOutcome.FailsAsync(api.Tasks.UpdateTaskAsync(request));

        Assert.Equal(StatusCode.InvalidArgument, failure.StatusCode);
        Assert.Equal(["a"], api.Store.Tasks.Single().Tags);
    }

    // Settings: behavior 38.

    [Fact]
    public async Task SetTagColor_normalizes_the_tag_it_names()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", tags: ["my-work"]));

        var settings = await api.SettingsCalls.SetTagColorAsync("  My   Work ", "purple-3");

        Assert.Equal([("my-work", "purple-3")], api.Store.TagColorsControl.Kept);
        Assert.Equal(("my-work", "purple-3", false), (settings.Tags.Single().Tag, settings.Tags.Single().Color, settings.Tags.Single().ColorIsDefault));
    }

    [Fact]
    public async Task MoveTag_normalizes_the_tags_it_names()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.AddRange([FakeTaskStore.Item("t1", tags: ["my-work", "home-page", "c"])]);
        api.Store.TagOrderControl.Seed("my-work", "home-page", "c");

        await api.SettingsCalls.MoveTagAsync(new V1.MoveTagRequest { Tag = "C", PreviousTag = " Home Page " });

        Assert.Equal([("c", "home-page", (string?)null)], api.Store.TagOrderControl.Moves);
    }

    [Fact]
    public async Task GetSettings_returns_each_tag_normalized()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", tags: ["Home Page", "WORK"]));

        var settings = await api.SettingsCalls.GetSettingsAsync();

        Assert.Equal(["home-page", "work"], settings.Tags.Select(look => look.Tag).Order());
    }
}
