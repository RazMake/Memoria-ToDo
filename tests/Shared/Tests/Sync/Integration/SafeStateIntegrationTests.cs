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
    [InlineData(Unsafe.OtherBranch, false)]
    [InlineData(Unsafe.DetachedHead, true)]
    [InlineData(Unsafe.UnmergedFiles, true)]
    [InlineData(Unsafe.ConflictedIndex, true)]
    public void Refuses_a_clone_that_is_detached_or_has_conflicts_but_not_one_on_another_branch(Unsafe state, bool refused)
    {
        var root = Path.Combine(Path.GetTempPath(), "memoria-safe-" + Guid.NewGuid().ToString("N"));
        try
        {
            var origin = OriginRepository.CreateWith(Path.Combine(root, "origin"), new Dictionary<string, string> { ["notes.txt"] = "seed" });
            var clone = Path.Combine(root, "clone");
            Repository.Clone(origin.Path, clone);
            MakeUnsafe(clone, state);
            using var repo = new Repository(clone);

            var failure = Record.Exception(() => SafeRepositoryState.Require(repo));

            Assert.Equal(refused, failure is not null);
            if (refused)
            {
                Assert.Equal(SyncFailure.Repository, Assert.IsType<GitFailureException>(failure).Kind);
            }
        }
        finally
        {
            SyncWorld.DeleteScratch(root);
        }
    }

    [Fact]
    public async Task Sync_refuses_a_detached_clone_and_changes_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var a = world.AddDevice("a");
        await a.ConnectAsync(ct);
        MakeUnsafe(a.CloneFolder, Unsafe.DetachedHead);
        a.AddTask("Local edit", "Must not be pushed");
        var before = a.Snapshot();
        var originBefore = world.Origin.MainTip;

        var status = await a.Sync.SyncNowAsync(ct).WaitAsync(Bound, ct);

        Assert.Equal(SyncFailure.Repository, status.Failure);
        Assert.Contains("detached", status.Details, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, a.Snapshot());
        Assert.Equal(originBefore, world.Origin.MainTip);
    }

    [Fact]
    public async Task Setup_refuses_a_detached_existing_clone_before_changing_its_origin_or_the_selection()
    {
        var state = Unsafe.DetachedHead;
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

    [Theory]
    [InlineData("g:repo.git", true)]
    [InlineData("git@g:repo.git", false)]
    public async Task Setup_clears_the_sync_receipt_only_when_it_actually_changes_the_origin_and_touches_nothing_else(string existingOrigin, bool receiptCleared)
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

        var unrelated = SetUnrelatedSettings(a.CloneFolder);

        // Needs no network: re-selecting the clone only rewrites its settings.
        await a.Sync.Setup.UseExistingAsync("g:repo.git", a.CloneFolder, ct).WaitAsync(Bound, ct);

        AssertOnlyOriginAddressChanged(a.CloneFolder, unrelated, "git@g:repo.git");
        using var after = new Repository(a.CloneFolder);
        Assert.Equal(receiptCleared, after.Config.Get<string>("memoria.lastSuccessfulSync", ConfigurationLevel.Local) is null);
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
