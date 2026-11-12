using LibGit2Sharp;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Sync only works in a repository that is on a branch, between Git operations, and without unresolved conflicts.</summary>
internal static class SafeRepositoryState
{
    /// <summary>Throws a Repository failure when the repository is detached, in the middle of a merge, rebase or similar, or has conflicted index entries.</summary>
    public static void Require(Repository repo)
    {
        // A conflicted index can outlive the operation that made it, so it is checked on its own.
        if (repo.Info.IsHeadDetached || repo.Info.CurrentOperation != CurrentOperation.None || repo.Index.Conflicts.Any())
        {
            throw new GitFailureException(SyncFailure.Repository, "The repository is detached, in the middle of another Git operation, or has unresolved conflicts.");
        }
    }
}
