using LibGit2Sharp;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Sync.Tests.Integration;

/// <summary>The one place that makes a clone's origin the canonical address, on a real repository and with no network.</summary>
[Integration]
public sealed class OriginRemoteIntegrationTests
{
    private const string Canonical = "git@g:repo.git";

    public enum Before { Missing, RawSingleLetter, AlreadyCanonical }

    [Theory]
    [InlineData(Before.Missing, true)]
    [InlineData(Before.RawSingleLetter, true)]
    [InlineData(Before.AlreadyCanonical, false)]
    public async Task Ensure_makes_the_origin_the_canonical_address_and_reports_whether_it_changed_it(Before before, bool changed)
    {
        await using var world = await SyncWorld.StartAsync(path => OriginRepository.CreateWith(path, new Dictionary<string, string> { ["notes.txt"] = "seed" }));
        var folder = Path.Combine(world.Root, "clone");
        Repository.Clone(world.Origin.Path, folder);
        using var repo = new Repository(folder);
        Arrange(repo, before);
        repo.Config.Set("test.keep", "unrelated", ConfigurationLevel.Local);
        repo.Network.Remotes.Add("other", "host.example:owner/other.git");
        var configBefore = File.ReadAllText(Path.Combine(folder, ".git", "config"));
        var refSpecs = before == Before.Missing ? null : repo.Network.Remotes["origin"].FetchRefSpecs.Select(r => r.Specification).ToArray();
        var pushUrl = before == Before.Missing ? null : repo.Network.Remotes["origin"].PushUrl;

        var (remote, didChange) = OriginRemote.Ensure(repo, Canonical);

        Assert.Equal(changed, didChange);
        Assert.Equal(Canonical, remote.Url);
        Assert.Equal(Canonical, repo.Network.Remotes["origin"].Url);
        Assert.Equal("unrelated", repo.Config.Get<string>("test.keep", ConfigurationLevel.Local)?.Value);
        Assert.Equal("host.example:owner/other.git", repo.Network.Remotes["other"].Url);
        if (refSpecs is not null)
        {
            Assert.Equal(refSpecs, repo.Network.Remotes["origin"].FetchRefSpecs.Select(r => r.Specification).ToArray());
            Assert.Equal(pushUrl, repo.Network.Remotes["origin"].PushUrl);
        }

        if (!changed)
        {
            Assert.Equal(configBefore, File.ReadAllText(Path.Combine(folder, ".git", "config")));
        }
    }

    private static void Arrange(Repository repo, Before before)
    {
        switch (before)
        {
            case Before.Missing:
                repo.Network.Remotes.Remove("origin");
                break;
            case Before.RawSingleLetter:
                repo.Network.Remotes.Update("origin", updater => updater.Url = "g:repo.git");
                repo.Config.Set("remote.origin.pushurl", "g:push.git", ConfigurationLevel.Local);
                break;
            case Before.AlreadyCanonical:
                repo.Network.Remotes.Update("origin", updater => updater.Url = Canonical);
                break;
        }
    }
}
