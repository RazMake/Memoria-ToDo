using LibGit2Sharp;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

[Integration]
public sealed class SafeStateIntegrationTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(60);

    public enum Unsafe { OtherBranch, DetachedHead, UnmergedFiles, ConflictedIndex }

    [Theory]
    [InlineData(Unsafe.OtherBranch)]
    [InlineData(Unsafe.DetachedHead)]
    [InlineData(Unsafe.UnmergedFiles)]
    public async Task Sync_refuses_a_clone_in_an_unsafe_state_and_changes_nothing(Unsafe state)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        MakeUnsafe(a.CloneFolder, state);
        a.AddTask("Local edit", "Must not be pushed");
        var before = a.Snapshot();
        var originBefore = world.Origin.MainTip;

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncFailure.Repository, status.Failure);
        Assert.Equal(before, a.Snapshot());
        Assert.Equal(originBefore, world.Origin.MainTip);
    }

    [Theory]
    [InlineData(Unsafe.DetachedHead)]
    [InlineData(Unsafe.UnmergedFiles)]
    [InlineData(Unsafe.ConflictedIndex)]
    public async Task Setup_refuses_an_existing_clone_in_an_unsafe_state_before_changing_its_origin_or_the_selection(Unsafe state)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAndSyncAsync(ct);

        // A second clone whose origin is not the server, so accepting it would rewrite its origin.
        var candidate = Path.Combine(world.Root, "candidate");
        Repository.Clone(world.Origin.Path, candidate);
        MakeUnsafe(candidate, state);
        var gitConfigBefore = File.ReadAllText(Path.Combine(candidate, ".git", "config"));
        var selectedBefore = a.Store.Synchronization.RepositoryFolder;
        var originBefore = world.Origin.MainTip;
        Assert.True(a.Sync.Setup.State.IsConfigured);

        await Assert.ThrowsAnyAsync<IOException>(() => a.Sync.Setup.UseExistingAsync(world.Address, candidate, ct).WaitAsync(Bound, ct));

        Assert.Equal(SyncFailure.Repository, a.Sync.Status.Failure);
        Assert.Equal(gitConfigBefore, File.ReadAllText(Path.Combine(candidate, ".git", "config")));
        Assert.Equal(selectedBefore, a.Store.Synchronization.RepositoryFolder);
        Assert.True(a.Sync.Setup.State.IsConfigured);
        Assert.Equal(originBefore, world.Origin.MainTip);
    }

    [Fact]
    public async Task Sync_refuses_an_unsafe_clone_whose_origin_is_a_bare_single_letter_address_before_saving_the_explicit_user_spelling()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        MakeUnsafe(a.CloneFolder, Unsafe.DetachedHead);
        using (var repo = new Repository(a.CloneFolder))
        {
            repo.Network.Remotes.Update("origin", updater => updater.Url = "g:origin.git");
        }

        var configBefore = File.ReadAllText(Path.Combine(a.CloneFolder, ".git", "config"));

        // Needs no network: the safety check runs before the pass contacts any host.
        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncFailure.Repository, status.Failure);
        Assert.Equal(configBefore, File.ReadAllText(Path.Combine(a.CloneFolder, ".git", "config")));
    }

    [Theory]
    [InlineData("g:repo.git", true)]
    [InlineData("git@g:repo.git", false)]
    public async Task Setup_clears_the_sync_receipt_only_when_it_actually_changes_the_origin(string existingOrigin, bool receiptCleared)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAndSyncAsync(ct);
        using (var repo = new Repository(a.CloneFolder))
        {
            repo.Network.Remotes.Update("origin", updater => updater.Url = existingOrigin);
            Assert.NotNull(repo.Config.Get<string>("memoria.lastSuccessfulSync", ConfigurationLevel.Local));
        }

        // Needs no network: re-selecting the clone only rewrites its settings.
        await a.Sync.Setup.UseExistingAsync("g:repo.git", a.CloneFolder, ct).WaitAsync(Bound, ct);

        using var after = new Repository(a.CloneFolder);
        Assert.Equal("git@g:repo.git", after.Network.Remotes["origin"].Url);
        Assert.Equal(receiptCleared, after.Config.Get<string>("memoria.lastSuccessfulSync", ConfigurationLevel.Local) is null);
    }

    [Fact]
    public async Task Setup_refuses_an_unsafe_clone_given_a_single_letter_scp_address_before_saving_the_explicit_user_spelling()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var candidate = Path.Combine(world.Root, "candidate");
        Repository.Clone(world.Origin.Path, candidate);
        MakeUnsafe(candidate, Unsafe.DetachedHead);
        var gitConfigBefore = File.ReadAllText(Path.Combine(candidate, ".git", "config"));
        var selectedBefore = a.Store.Synchronization.RepositoryFolder;

        // Needs no network: the refusal comes before anything contacts host "g".
        await Assert.ThrowsAnyAsync<IOException>(() => a.Sync.Setup.UseExistingAsync("g:origin.git", candidate, ct).WaitAsync(Bound, ct));

        Assert.Equal(SyncFailure.Repository, a.Sync.Status.Failure);
        Assert.Equal(gitConfigBefore, File.ReadAllText(Path.Combine(candidate, ".git", "config")));
        Assert.Equal(selectedBefore, a.Store.Synchronization.RepositoryFolder);
    }

    [Theory]
    [InlineData("g:repo.git", "git@g:repo.git")]
    [InlineData("G:dir/sub/repo.git", "git@G:dir/sub/repo.git")]
    [InlineData("deploy@g:repo.git", "deploy@g:repo.git")]
    [InlineData("host.example:owner/repo.git", "host.example:owner/repo.git")]
    [InlineData("ssh://g/repo.git", "ssh://g/repo.git")]
    public async Task Setup_saves_a_bare_single_letter_scp_address_with_its_user_and_keeps_every_other_address_as_typed(string typed, string saved)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var candidate = Path.Combine(world.Root, "candidate");
        Repository.Clone(world.Origin.Path, candidate);
        var unrelated = SetUnrelatedSettings(candidate);

        // Needs no network: UseExistingAsync only validates and rewrites the clone's settings.
        await a.Sync.Setup.UseExistingAsync(typed, candidate, ct).WaitAsync(Bound, ct);

        AssertOnlyOriginAddressChanged(candidate, unrelated, saved);
        Assert.Equal(candidate, a.Store.Synchronization.RepositoryFolder);
    }

    [Fact]
    public async Task Setup_replaces_an_existing_bare_single_letter_origin_with_the_explicit_user_spelling_permanently()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        var candidate = Path.Combine(world.Root, "candidate");
        Repository.Clone(world.Origin.Path, candidate);
        using (var repo = new Repository(candidate))
        {
            repo.Network.Remotes.Update("origin", updater => updater.Url = "g:repo.git");
        }

        var unrelated = SetUnrelatedSettings(candidate);

        await a.Sync.Setup.UseExistingAsync("g:repo.git", candidate, ct).WaitAsync(Bound, ct);

        AssertOnlyOriginAddressChanged(candidate, unrelated, "git@g:repo.git");
        // The same address again leaves the explicit spelling in place.
        await a.Sync.Setup.UseExistingAsync("g:repo.git", candidate, ct).WaitAsync(Bound, ct);
        AssertOnlyOriginAddressChanged(candidate, unrelated, "git@g:repo.git");
    }

    /// <summary>Adds settings that setting the origin must not touch: another remote and a custom key.</summary>
    private static (string Key, string Value, string OtherUrl, string[] FetchRefSpecs) SetUnrelatedSettings(string clone)
    {
        using var repo = new Repository(clone);
        repo.Config.Set("test.keep", "unrelated", ConfigurationLevel.Local);
        repo.Network.Remotes.Add("other", "host.example:owner/other.git");
        return ("test.keep", "unrelated", "host.example:owner/other.git", repo.Network.Remotes["origin"].FetchRefSpecs.Select(r => r.Specification).ToArray());
    }

    private static void AssertOnlyOriginAddressChanged(string clone, (string Key, string Value, string OtherUrl, string[] FetchRefSpecs) unrelated, string expectedOrigin)
    {
        using var repo = new Repository(clone);
        Assert.Equal(expectedOrigin, repo.Network.Remotes["origin"].Url);
        Assert.Equal(unrelated.Value, repo.Config.Get<string>(unrelated.Key, ConfigurationLevel.Local)?.Value);
        Assert.Equal(unrelated.OtherUrl, repo.Network.Remotes["other"].Url);
        Assert.Equal(unrelated.FetchRefSpecs, repo.Network.Remotes["origin"].FetchRefSpecs.Select(r => r.Specification).ToArray());
    }

    private static void MakeUnsafe(string clone, Unsafe state)
    {
        using var repo = new Repository(clone);
        var signature = new Signature("Test", "test@example.org", DateTimeOffset.UtcNow);
        switch (state)
        {
            case Unsafe.OtherBranch:
                Commands.Checkout(repo, repo.CreateBranch("feature"));
                break;
            case Unsafe.DetachedHead:
                Commands.Checkout(repo, repo.Head.Tip);
                break;
            case Unsafe.UnmergedFiles:
            case Unsafe.ConflictedIndex:
                var side = repo.CreateBranch("side");
                Commands.Checkout(repo, side);
                Commit(repo, signature, "notes.txt", "side");
                Commands.Checkout(repo, repo.Branches["main"]);
                Commit(repo, signature, "notes.txt", "main");
                Assert.Equal(MergeStatus.Conflicts, repo.Merge(side, signature).Status);
                if (state == Unsafe.ConflictedIndex)
                {
                    // The merge's own bookkeeping is gone, as after `git merge --quit`, but the index still holds the conflict.
                    foreach (var name in new[] { "MERGE_HEAD", "MERGE_MSG", "MERGE_MODE" })
                    {
                        File.Delete(Path.Combine(repo.Info.Path, name));
                    }
                }

                break;
        }

        if (state == Unsafe.ConflictedIndex)
        {
            using var reopened = new Repository(clone);
            Assert.True(reopened.Index.Conflicts.Any());
            Assert.Equal(CurrentOperation.None, reopened.Info.CurrentOperation);
        }
    }

    private static void Commit(Repository repo, Signature signature, string file, string text)
    {
        File.WriteAllText(Path.Combine(repo.Info.WorkingDirectory, file), text);
        Commands.Stage(repo, file);
        repo.Commit(text, signature, signature);
    }
}
