using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

[Integration]
public sealed class CloneAndMergeIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Clone_selects_the_folder_and_a_task_added_on_one_device_reaches_another()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        const string deviceName = "pixel-7";
        var a = world.AddDevice(deviceName);
        var b = world.AddDevice("b");
        await a.ConnectAsync(ct);
        await b.ConnectAsync(ct);
        Assert.Equal(a.CloneFolder, a.Sync.Setup.State.RepositoryFolder);

        var task = a.AddTask("Buy milk", "Two litres");
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        // Owner (sync.md, pass steps and commit identity): the commit names the device, lists each changed managed path with its action.
        var added = world.Origin.MainTipCommit!.Value;
        Assert.Equal(deviceName, added.AuthorName);
        Assert.Equal("device@memoria.invalid", added.AuthorEmail);
        Assert.Contains(deviceName, added.Message.Split('\n')[0]);
        Assert.Contains($"Add: tasks/{task.Id}.json", added.Message);

        a.EditTask(task.Id, description: "Three litres");
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        var updated = world.Origin.MainTipCommit!.Value;
        Assert.Equal(deviceName, updated.AuthorName);
        Assert.Contains($"Update: tasks/{task.Id}.json", updated.Message);

        await b.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.NotNull(world.Origin.ReadOnMain($"tasks/{task.Id}.json"));
        Assert.Equal("Three litres", b.Item(task.Id).Description);
    }

    [Fact]
    public async Task Edits_to_different_fields_of_one_task_on_two_devices_are_both_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        var a = world.AddDevice("a");
        var b = world.AddDevice("b");
        await a.ConnectAsync(ct);
        await b.ConnectAsync(ct);
        var task = a.AddTask("Title", "Description");
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        await b.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        a.EditTask(task.Id, description: "Edited on A");
        b.EditTask(task.Id, title: "Edited on B");
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        await b.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        foreach (var device in new[] { a, b })
        {
            Assert.Equal("Edited on B", device.Item(task.Id).Title);
            Assert.Equal("Edited on A", device.Item(task.Id).Description);
        }
    }

    [Fact]
    public async Task A_first_sync_creates_main_on_an_origin_that_has_no_commits()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync();
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        Assert.Null(world.Origin.MainTip);

        var task = a.AddTask("First", "On an empty origin");
        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Idle, status.State);
        Assert.NotNull(world.Origin.ReadOnMain($"tasks/{task.Id}.json"));
    }

    [Fact]
    public async Task A_push_that_loses_a_race_is_retried_and_succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes/0.txt"] = "0" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var rejections = 0;
        world.Server.BeforeCommand = (verb, _) =>
        {
            if (verb == "receive-pack" && Interlocked.Increment(ref rejections) <= 2)
                world.Origin.Commit(new Dictionary<string, string> { [$"notes/{rejections}.txt"] = "x" }, "Other device pushed first");
            return Task.CompletedTask;
        };

        var task = a.AddTask("Raced", "Pushed after two rejections");
        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Idle, status.State);
        var commands = world.Server.Commands.ToArray();
        Assert.True(commands.Count(c => c == "receive-pack") == 3, $"receive-pack commands: {string.Join(", ", commands)}; failure {status.Failure}.");
        Assert.NotNull(world.Origin.ReadOnMain($"tasks/{task.Id}.json"));
    }

    [Fact]
    public async Task A_push_that_keeps_losing_the_race_stops_after_three_rejected_pushes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes/0.txt"] = "0" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var rejections = 0;
        world.Server.BeforeCommand = (verb, _) =>
        {
            if (verb == "receive-pack")
                world.Origin.Commit(new Dictionary<string, string> { [$"notes/{Interlocked.Increment(ref rejections)}.txt"] = "x" }, "Other device pushed first");
            return Task.CompletedTask;
        };

        a.AddTask("Never lands", "Always loses");
        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncFailure.PushRejected, status.Failure);
        var commands = world.Server.Commands.ToArray();
        Assert.True(commands.Count(c => c == "receive-pack") == 3, $"receive-pack commands: {string.Join(", ", commands)}.");
    }

    [Fact]
    public async Task A_push_the_host_declines_for_a_reason_other_than_a_race_keeps_the_hosts_reason_in_the_failure_details()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes/0.txt"] = "0" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);

        // A real pre-receive hook on the origin declines every push, as a host's branch policy would.
        var hooks = Directory.CreateDirectory(Path.Combine(world.Origin.Path, "hooks")).FullName;
        await File.WriteAllTextAsync(Path.Combine(hooks, "pre-receive"), "#!/bin/sh\necho 'policy: pushes are closed' >&2\nexit 1\n", ct);
        a.AddTask("Declined", "The host's hook refuses it");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncFailure.PushRejected, status.Failure);
        var commands = world.Server.Commands.ToArray();
        Assert.True(commands.Count(c => c == "receive-pack") == 3, $"receive-pack commands: {string.Join(", ", commands)}.");
        Assert.Contains("pre-receive hook declined", status.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(world.Origin.FilesOnMain("tasks"));
    }
}
