using LibGit2Sharp;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// Sync never discards local work, except the two cases the user allowed in docs/architecture/sync.md: a staged-only
/// unmanaged version, and a malformed task the remote deleted. An uncommitted edit to an unmanaged file survives a merge.
/// </summary>
[Integration]
public sealed class LocalWorkIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);
    private static readonly Dictionary<string, string> Seed = new() { ["README.md"] = "seed readme" };

    [Fact]
    public async Task An_uncommitted_edit_to_an_unmanaged_file_survives_when_the_remote_changes_only_a_task()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed));
        var a = world.AddDevice("a");
        var b = world.AddDevice("b");
        await a.ConnectAsync(ct);
        await b.ConnectAsync(ct);
        var remoteTask = b.AddTask("From b", "Pushed first");
        await b.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        var readme = Path.Combine(a.CloneFolder, "README.md");
        await File.WriteAllTextAsync(readme, "my unsaved notes", ct);
        var localTask = a.AddTask("From a", "Committed by the sync");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal("my unsaved notes", await File.ReadAllTextAsync(readme, ct));
        Assert.NotNull(a.Item(localTask.Id));
        if (status.State == SyncState.Error)
            Assert.Equal(SyncFailure.Repository, status.Failure);
        else
            Assert.Equal("Pushed first", a.Item(remoteTask.Id).Description);
    }

    [Fact]
    public async Task An_unmanaged_file_with_no_local_edit_follows_the_remote()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        world.Origin.Commit(new Dictionary<string, string> { ["README.md"] = "remote readme" }, "Edit README elsewhere");
        a.AddTask("Local", "Forces a merge");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Idle, status.State);
        Assert.Equal("remote readme", await File.ReadAllTextAsync(Path.Combine(a.CloneFolder, "README.md"), ct));
    }

    [Fact]
    public async Task An_unmanaged_edit_that_exists_only_in_the_staging_area_yields_to_the_remote_version()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var readme = Path.Combine(a.CloneFolder, "README.md");
        await File.WriteAllTextAsync(readme, "staged only", ct);
        using (var repo = new Repository(a.CloneFolder))
            Commands.Stage(repo, "README.md");
        await File.WriteAllTextAsync(readme, "seed readme", ct);
        world.Origin.Commit(new Dictionary<string, string> { ["README.md"] = "remote readme" }, "Edit README elsewhere");
        a.AddTask("Local", "Forces a merge");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        // The user decided the staged-only version is not protected: the remote's result is applied.
        Assert.Equal(SyncState.Idle, status.State);
        Assert.Equal("remote readme", await File.ReadAllTextAsync(readme, ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_task_edited_on_one_device_and_deleted_on_the_other_comes_back_with_its_images(bool thisDeviceEdits)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed));
        var a = world.AddDevice("a");
        var b = world.AddDevice("b");
        await a.ConnectAsync(ct);
        await b.ConnectAsync(ct);
        var task = b.AddTask("Shared", "original");
        var imagePath = $"images/{task.Id}/{Guid.CreateVersion7()}.png";
        var imageBytes = new byte[] { 1, 2, 3, 4, 5 };
        var imageOnB = Path.Combine(b.CloneFolder, imagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(imageOnB)!);
        await File.WriteAllBytesAsync(imageOnB, imageBytes, ct);
        await b.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);
        var imageOnA = Path.Combine(a.CloneFolder, imagePath);
        Assert.Equal(imageBytes, await File.ReadAllBytesAsync(imageOnA, ct));

        var (editor, deleter) = thisDeviceEdits ? (a, b) : (b, a);
        editor.EditTask(task.Id, description: "edited");
        deleter.Store.Delete(task.Id);
        Assert.False(File.Exists(Path.Combine(deleter.CloneFolder, imagePath)));
        // The one that syncs second is "this device", whose merge meets the other's change as the remote's.
        var (first, second) = thisDeviceEdits ? (deleter, editor) : (editor, deleter);
        Assert.Equal(SyncState.Idle, (await first.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct)).State);
        Assert.Equal(SyncState.Idle, (await second.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct)).State);

        Assert.Equal("edited", second.Item(task.Id).Description);
        Assert.Equal(imageBytes, await File.ReadAllBytesAsync(Path.Combine(second.CloneFolder, imagePath), ct));
        Assert.Contains("edited", world.Origin.ReadOnMain($"tasks/{task.Id}.json"));
        Assert.NotNull(world.Origin.ReadOnMain(imagePath));
    }

    [Fact]
    public async Task An_ignored_local_file_that_the_remote_now_tracks_is_never_overwritten_and_the_sync_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(
            path, new Dictionary<string, string> { ["README.md"] = "seed readme", [".gitignore"] = "scratch.txt\n" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var scratch = Path.Combine(a.CloneFolder, "scratch.txt");
        await File.WriteAllTextAsync(scratch, "my ignored notes", ct);
        world.Origin.Commit(new Dictionary<string, string> { ["scratch.txt"] = "remote scratch" }, "Track the file elsewhere");
        a.AddTask("Local", "Forces a merge");

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal("my ignored notes", await File.ReadAllTextAsync(scratch, ct));
        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.Repository, status.Failure);
    }
}
