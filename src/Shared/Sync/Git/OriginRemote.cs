using LibGit2Sharp;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>The one place that makes a repository's <c>origin</c> remote point at a given address.</summary>
internal static class OriginRemote
{
    /// <summary>
    /// Adds <c>origin</c>, or updates its URL, so it is <paramref name="url"/>; other remote settings stay.
    /// Returns the current remote, read back after any change, and whether it was changed.
    /// </summary>
    public static (Remote Remote, bool Changed) Ensure(Repository repo, string url)
    {
        var origin = repo.Network.Remotes["origin"];
        if (origin is null)
        {
            return (repo.Network.Remotes.Add("origin", url), true);
        }

        if (origin.Url == url)
        {
            return (origin, false);
        }

        repo.Network.Remotes.Update("origin", updater => updater.Url = url);
        return (repo.Network.Remotes["origin"], true);
    }
}
