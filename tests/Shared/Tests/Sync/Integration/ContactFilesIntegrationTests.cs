using Memoria.ToDo.Shared.Contacts.Tests;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>
/// Sync carries Contacts' files like task files (docs/architecture/sync.md): it commits them, merges them by Contacts' rules
/// on two real devices, and leaves other files under autocomplete/ as the remote has them. The contact files are written
/// as literal JSON, as a user's editor would.
/// </summary>
[Integration]
public sealed class ContactFilesIntegrationTests
{
    private const string Team = "autocomplete/contacts/team.json";
    private const string Notes = "autocomplete/notes.md";
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    private static string Ana(params (string Key, string Value)[] fields) =>
        ContactFiles.Group(1000, ContactFiles.Person("1", "Ana", fields));

    // Contacts reports a missing data type file, which would fail every sync, so each seeded clone has both.
    private static Dictionary<string, string> Seed(params (string Path, string Text)[] files) =>
        new[]
        {
            (Team, Ana(("title", "Dev"))),
            ("autocomplete/contacts/dataTypes/pronouns.json", ContactFiles.ArrayOf()),
            ("autocomplete/contacts/dataTypes/careerPaths.json", ContactFiles.ArrayOf()),
        }.Concat(files).ToDictionary(file => file.Item1, file => file.Item2);

    private static void Write(Device device, string path, string text)
    {
        var full = Path.Combine(device.CloneFolder, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    private static IReadOnlyDictionary<string, string> AnaOnDisk(Device device) =>
        ContactFiles.Read(File.ReadAllBytes(Path.Combine(device.CloneFolder, "autocomplete", "contacts", "team.json"))).WithId("1");

    private static async Task<Device> ConfiguredAsync(SyncWorld world, string name)
    {
        var device = world.AddDevice(name);
        await device.ConnectAndSyncAsync(TestContext.Current.CancellationToken);
        return device;
    }

    [Fact]
    public async Task Edits_to_different_fields_of_one_contact_on_two_devices_are_both_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed()));
        var a = await ConfiguredAsync(world, "a");
        var b = await ConfiguredAsync(world, "b");
        Write(a, Team, Ana(("title", "Lead")));
        Write(b, Team, Ana(("title", "Dev"), ("location", "Cluj")));

        Assert.Equal(SyncState.Idle, (await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct)).State);
        Assert.Equal(SyncState.Idle, (await b.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct)).State);
        Assert.Equal(SyncState.Idle, (await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct)).State);

        var onOrigin = ContactFiles.Read(world.Origin.ReadBytesOnMain(Team)!).WithId("1");
        foreach (var ana in new[] { onOrigin, AnaOnDisk(a), AnaOnDisk(b) })
        {
            Assert.Equal("Lead", ana["title"]);
            Assert.Equal("Cluj", ana["location"]);
        }
    }

    [Fact]
    public async Task Commit_messages_list_contact_paths_with_their_action()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed()));
        var a = await ConfiguredAsync(world, "a");
        const string other = "autocomplete/contacts/other.json";
        Write(a, Team, Ana(("title", "Lead")));
        Write(a, other, ContactFiles.Group(2000, ContactFiles.Person("2", "Bob")));

        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        var committed = world.Origin.MainTipCommit!.Value.Message;
        Assert.Contains($"Update: {Team}", committed);
        Assert.Contains($"Add: {other}", committed);

        File.Delete(Path.Combine(a.CloneFolder, "autocomplete", "contacts", "other.json"));
        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Contains($"Delete: {other}", world.Origin.MainTipCommit!.Value.Message);
        Assert.Null(world.Origin.ReadOnMain(other));
    }

    [Fact]
    public async Task A_local_contact_edit_is_committed_and_does_not_block_a_merge_while_other_autocomplete_files_follow_the_remote()
    {
        var ct = TestContext.Current.CancellationToken;
        const string unknownType = "autocomplete/contacts/dataTypes/unknown.json";
        const string deeper = "autocomplete/contacts/deeper/team.json";
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(
            path, Seed((Notes, "notes v1"), (unknownType, "[]"), (deeper, "{}"))));
        var a = await ConfiguredAsync(world, "a");
        world.Origin.Commit(
            new Dictionary<string, string> { [Notes] = "notes v2", [unknownType] = "[1]", [deeper] = "{\"v\":2}" }, "Edit the other files elsewhere");
        Write(a, Team, Ana(("title", "Lead")));

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Idle, status.State);
        Assert.Empty(status.SkippedFiles);
        Assert.Equal("Lead", AnaOnDisk(a)["title"]);
        Assert.Equal("Lead", ContactFiles.Read(world.Origin.ReadBytesOnMain(Team)!).WithId("1")["title"]);
        Assert.Equal("notes v2", File.ReadAllText(Path.Combine(a.CloneFolder, "autocomplete", "notes.md")));
        Assert.Equal("[1]", File.ReadAllText(Path.Combine(a.CloneFolder, "autocomplete", "contacts", "dataTypes", "unknown.json")));
        Assert.Equal("{\"v\":2}", File.ReadAllText(Path.Combine(a.CloneFolder, "autocomplete", "contacts", "deeper", "team.json")));
        Assert.Equal("notes v2", world.Origin.ReadOnMain(Notes));
    }

    [Fact]
    public async Task An_uncommitted_edit_to_another_autocomplete_file_is_not_committed_and_survives_a_sync_with_nothing_new_from_the_remote()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed((Notes, "notes v1"))));
        var a = await ConfiguredAsync(world, "a");
        Write(a, Notes, "my notes");
        Write(a, Team, Ana(("title", "Lead")));

        await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal("my notes", File.ReadAllText(Path.Combine(a.CloneFolder, "autocomplete", "notes.md")));
        Assert.Equal("notes v1", world.Origin.ReadOnMain(Notes));
        Assert.DoesNotContain("autocomplete/notes.md", world.Origin.MainTipCommit!.Value.Message);
        Assert.Contains($"Update: {Team}", world.Origin.MainTipCommit!.Value.Message);
    }

    [Fact]
    public async Task An_uncommitted_contact_edit_counts_as_a_pending_change_when_the_sync_fails()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed()));
        var a = await ConfiguredAsync(world, "a");
        world.Server.AllowPush = false;
        world.Server.DenialText = "fatal: Permission denied";
        Write(a, Team, Ana(("title", "Lead")));

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncState.Error, status.State);
        Assert.True(status.HasPendingChanges);
        Assert.Equal("Dev", ContactFiles.Read(world.Origin.ReadBytesOnMain(Team)!).WithId("1")["title"]);
    }

    [Fact]
    public async Task An_uncommitted_contact_edit_counts_as_a_pending_change_when_the_sync_fails_before_committing_anything()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, Seed()));
        var a = await ConfiguredAsync(world, "a");
        var tipBefore = world.Origin.MainTip;
        CheckOutAnotherBranchTheCloneTreatsAsPublished(a);
        Write(a, Team, Ana(("title", "Lead")));

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        // The pass stops on the branch mismatch before it commits anything, so the contact edit is the only change left.
        Assert.Equal(SyncState.Error, status.State);
        Assert.Equal(SyncFailure.Repository, status.Failure);
        Assert.True(status.HasPendingChanges);
        Assert.Equal(tipBefore, world.Origin.MainTip);
    }

    // Checks out a branch other than the remote's default, with a remote-tracking ref at the same commit, so the clone is not
    // ahead of the remote and the pass fails resolving the branch, before it commits.
    private static void CheckOutAnotherBranchTheCloneTreatsAsPublished(Device device)
    {
        using var repo = new LibGit2Sharp.Repository(device.CloneFolder);
        var tip = repo.Head.Tip;
        LibGit2Sharp.Commands.Checkout(repo, repo.Branches.Add("feature", tip));
        repo.Refs.Add("refs/remotes/origin/feature", tip.Id);
    }
}
