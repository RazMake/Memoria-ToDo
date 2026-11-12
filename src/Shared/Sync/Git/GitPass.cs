using LibGit2Sharp;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>One sync pass on the worker: check, delete, commit, then fetch, merge and push until the remote accepts.</summary>
internal sealed class GitPass(ITaskSynchronization tasks, TimeProvider time, string folder, GitConnection connection, CancellationToken token)
{
    private const int MaxPushAttempts = 3;
    private const string FallbackBranch = "main";

    public GitPassResult Run()
    {
        using var repo = OpenRepository();
        var saved = repo.Network.Remotes["origin"]
            ?? throw new GitFailureException(SyncFailure.Repository, "The repository has no 'origin' remote.");
        SafeRepositoryState.Require(repo);
        var remote = Canonical(repo, saved);

        var branch = ResolveBranch(repo, remote, out var remoteHasBranch);
        PrepareFiles();
        var commit = new ManagedCommit(repo, tasks.Files, time, connection.DeviceName);
        var merge = new DomainMerge(repo, tasks.Files, time, connection.DeviceName);
        tasks.UpdateRepository(folder, () => commit.Run());

        for (var attempt = 1; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            Commit? tip = null;
            if (remoteHasBranch)
            {
                Fetch(repo, branch);
                tip = repo.Branches[$"origin/{branch}"]?.Tip;
                tasks.UpdateRepository(folder, () =>
                {
                    commit.Run();
                    if (tip is not null && repo.Head.Tip is { } local)
                    {
                        merge.Run(local, tip, branch);
                    }
                });
            }

            var head = repo.Head.Tip;
            if (head is null || head.Id == tip?.Id)
            {
                // Nothing to push, so a push can't prove the key may write: check it, so a read-only or revoked key shows.
                WriteAccessCheck.Run(AddressParser.Parse(remote.Url));
            }

            string? reason = null;
            if (head is null || head.Id == tip?.Id || Push(repo, remote, branch, out reason))
            {
                return new GitPassResult(commit.HasManagedChanges()) { Skipped = merge.Skipped };
            }

            remoteHasBranch = true;
            if (attempt == MaxPushAttempts)
            {
                var detail = reason is null ? string.Empty : $" The host said: {reason}";
                throw new GitFailureException(SyncFailure.PushRejected, $"The remote rejected the push {MaxPushAttempts} times.{detail}");
            }
        }
    }

    private Repository OpenRepository() =>
        Repository.IsValid(folder)
            ? new Repository(folder)
            : throw new GitFailureException(SyncFailure.Repository, "The selected folder is not a Git repository.");

    private static Remote Canonical(Repository repo, Remote saved)
    {
        string canonical;
        try
        {
            canonical = AddressParser.Parse(saved.Url).Text;
        }
        catch (ArgumentException ex)
        {
            throw new GitFailureException(SyncFailure.Repository, $"The repository's 'origin' address is not a usable SSH address. {ex.Message}", ex);
        }

        return OriginRemote.Ensure(repo, canonical).Remote;
    }

    // Decides the branch before any file changes: an empty remote takes the current branch, or main for a new one.
    private string ResolveBranch(Repository repo, Remote remote, out bool remoteHasBranch)
    {
        var current = repo.Head.FriendlyName;
        var defaultBranch = RemoteBranch.Find(repo.Network.ListReferences(remote), current);
        remoteHasBranch = defaultBranch is not null;
        if (defaultBranch is null)
        {
            if (repo.Head.Tip is null && current != FallbackBranch)
            {
                repo.Refs.Add("HEAD", $"refs/heads/{FallbackBranch}", allowOverwrite: true);
            }

            return repo.Head.FriendlyName;
        }

        if (defaultBranch == current)
        {
            return current;
        }

        if (repo.Head.Tip is not null)
        {
            throw new GitFailureException(SyncFailure.Repository, $"The checked out branch '{current}' is not the remote's default branch '{defaultBranch}'.");
        }

        repo.Refs.Add("HEAD", $"refs/heads/{defaultBranch}", allowOverwrite: true);
        return defaultBranch;
    }

    private void PrepareFiles()
    {
        try
        {
            tasks.EnsureSettings();
            tasks.DeleteExpiredTasks();
        }
        catch (InvalidDataException ex)
        {
            throw new GitFailureException(SyncFailure.InvalidFile, ex.Message, ex);
        }
    }

    private void Fetch(Repository repo, string branch) =>
        Commands.Fetch(repo, "origin", [$"+refs/heads/{branch}:refs/remotes/origin/{branch}"],
            new FetchOptions { TagFetchMode = TagFetchMode.None, OnTransferProgress = _ => !token.IsCancellationRequested }, null);

    // Returns false when the remote rejected the push because it moved on.
    // The host's reason, when it gave one, is returned in reason.
    private bool Push(Repository repo, Remote remote, string branch, out string? reason)
    {
        var rejected = false;
        string? hostReason = null;
        try
        {
            repo.Network.Push(remote, $"refs/heads/{branch}:refs/heads/{branch}", new PushOptions
            {
                OnPushStatusError = error =>
                {
                    rejected = true;
                    hostReason = error.Message;
                },
            });
        }
        catch (NonFastForwardException)
        {
            rejected = true;
        }

        reason = hostReason;
        return !rejected;
    }
}
