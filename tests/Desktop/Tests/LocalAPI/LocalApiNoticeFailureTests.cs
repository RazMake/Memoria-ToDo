// These calls run in-process on fakes and never wait on a network, so the test's own cancellation token isn't needed.
#pragma warning disable xUnit1051

using System.IO;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;
using V1 = Memoria.ToDo.Api.V1;

namespace Memoria.ToDo.Desktop.LocalAPI.Tests;

/// <summary>
/// A change Tasks saved before a notice failed ends the call with its normal success reply, built from the exception's saved item,
/// never INTERNAL or INVALID_ARGUMENT, and the failure is logged once at Error with the exception
/// (docs/architecture/local-api-contract.md, "Failure modes").
/// </summary>
[Unit]
public class LocalApiNoticeFailureTests
{
    private static readonly DateTimeOffset Base = new(2026, 3, 2, 10, 0, 0, TimeSpan.Zero);

    private static NoticeFailedException<TaskItem> Failure(TaskItem? saved) => new(saved, [new IOException("Changed failed.")]);

    private static async Task AssertLoggedAsync(ApiHarness api, string method)
    {
        var entry = await api.Log.EntryAsync(LogEventLevel.Error, method);
        Assert.IsAssignableFrom<NoticeFailedException>(entry.Exception);
    }

    [Fact]
    public async Task CreateTask_returns_the_new_task_with_its_ID_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        var saved = FakeTaskStore.Item("new-1", title: "Buy milk");
        api.Store.Failure = Failure(saved);

        var created = await api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "Buy milk" });

        Assert.Equal("new-1", created.Id);
        Assert.Equal("Buy milk", created.Title);
        await AssertLoggedAsync(api, "CreateTask");
    }

    [Fact]
    public async Task CreateTask_logs_its_own_entry_as_an_OK_Information_and_adds_no_INTERNAL_status()
    {
        await using var api = new ApiHarness();
        api.Store.Failure = Failure(FakeTaskStore.Item("new-1", title: "Buy milk"));

        await api.Tasks.CreateTaskAsync(new V1.CreateTaskRequest { Title = "Buy milk" });

        var entry = await api.Log.EntryAsync(LogEventLevel.Information, "CreateTask");
        Assert.True(entry.NamesStatus(Grpc.Core.StatusCode.OK));
    }

    [Fact]
    public async Task UpdateTask_returns_the_saved_task_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", title: "Old", created: Base, updated: Base));
        api.Store.Failure = Failure(FakeTaskStore.Item("t1", title: "New", created: Base, updated: Base.AddHours(1)));

        var updated = await api.Tasks.UpdateTaskAsync(new V1.UpdateTaskRequest { Id = "t1", BaseUpdated = Timestamp.FromDateTimeOffset(Base), Title = "New" });

        Assert.Equal("New", updated.Title);
        await AssertLoggedAsync(api, "UpdateTask");
    }

    [Fact]
    public async Task AddImage_returns_the_saved_task_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1", title: "Chart"));
        api.Store.Failure = Failure(FakeTaskStore.Item("t1", title: "Chart", description: "![A chart](images/x.png)"));

        var changed = await api.Tasks.AddImageAsync(new V1.AddImageRequest { Id = "t1", Image = ByteString.CopyFrom(1), AltText = "A chart" });

        Assert.Equal("t1", changed.Id);
        await AssertLoggedAsync(api, "AddImage");
    }

    [Fact]
    public async Task MoveTask_returns_the_saved_task_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1"));
        api.Store.Failure = Failure(FakeTaskStore.Item("t1"));

        var moved = await api.Tasks.MoveTaskAsync(new V1.MoveTaskRequest { Id = "t1" });

        Assert.Equal("t1", moved.Id);
        await AssertLoggedAsync(api, "MoveTask");
    }

    [Fact]
    public async Task ResolveTask_returns_the_saved_task_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1"));
        api.Store.Failure = Failure(FakeTaskStore.Item("t1"));

        var resolved = await api.Tasks.ResolveTaskAsync(new V1.ResolveTaskRequest { Id = "t1" });

        Assert.Equal("t1", resolved.Id);
        await AssertLoggedAsync(api, "ResolveTask");
    }

    [Fact]
    public async Task ReopenTask_returns_the_saved_task_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1"));
        api.Store.Failure = Failure(FakeTaskStore.Item("t1"));

        var reopened = await api.Tasks.ReopenTaskAsync(new V1.ReopenTaskRequest { Id = "t1" });

        Assert.Equal("t1", reopened.Id);
        await AssertLoggedAsync(api, "ReopenTask");
    }

    [Fact]
    public async Task DeleteTask_answers_normally_when_a_notice_failed_after_the_delete()
    {
        await using var api = new ApiHarness();
        api.Store.Tasks.Add(FakeTaskStore.Item("t1"));
        api.Store.Failure = Failure(null);

        var response = await api.Tasks.DeleteTaskAsync(new V1.DeleteTaskRequest { Id = "t1" });

        Assert.NotNull(response);
        await AssertLoggedAsync(api, "DeleteTask");
    }

    [Fact]
    public async Task SetDeletionDelay_returns_the_settings_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        api.Store.SettingsControl.SetFailure = Failure(null);

        var settings = await api.SettingsCalls.SetDeletionDelayAsync(45);

        Assert.NotNull(settings);
        await AssertLoggedAsync(api, "SetDeletionDelay");
    }

    [Fact]
    public async Task SetTagColor_returns_the_settings_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        api.Store.TagColorsControl.Failure = Failure(null);

        var settings = await api.SettingsCalls.SetTagColorAsync("home", "red-1");

        Assert.NotNull(settings);
        await AssertLoggedAsync(api, "SetTagColor");
    }

    [Fact]
    public async Task MoveTag_returns_the_settings_when_a_notice_failed_after_the_save()
    {
        await using var api = new ApiHarness();
        api.Store.TagOrderControl.Seed("a", "b");
        api.Store.TagOrderControl.SavesBeforeFailing = true;
        api.Store.TagOrderControl.Failure = Failure(null);

        var settings = await api.SettingsCalls.MoveTagAsync(new V1.MoveTagRequest { Tag = "a", NextTag = "b" });

        Assert.NotNull(settings);
        await AssertLoggedAsync(api, "MoveTag");
    }

    [Fact]
    public async Task MoveGroup_returns_the_groups_when_a_notice_failed_after_the_move()
    {
        await using var api = new ApiHarness();
        api.SeedStandard();
        api.Contacts.Changed += (_, _) => throw new IOException("Changed failed.");

        var groups = await api.ContactCalls.MoveGroupAsync(new V1.MoveGroupRequest { Group = "team", PreviousGroup = "alumni" });

        Assert.NotNull(groups);
        await AssertLoggedAsync(api, "MoveGroup");
    }
}
