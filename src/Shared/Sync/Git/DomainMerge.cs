using LibGit2Sharp;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Joins the local branch with the fetched remote tip. Each file is merged by the Tasks or Contacts rules for its kind; files
/// Sync doesn't manage stay as the remote has them.
/// </summary>
internal sealed class DomainMerge(Repository repo, IRepositoryFileRules rules, IContactFileRules contacts, TimeProvider time, string deviceName)
{
    private readonly ManagedFiles managed = new(rules, contacts);
    private readonly Dictionary<string, ISkippedRepositoryFile> skipped = new(StringComparer.Ordinal);

    /// <summary>The files whose local copy was kept because they couldn't be merged, one per path.</summary>
    public IReadOnlyList<ISkippedRepositoryFile> Skipped => [.. skipped.Values];

    /// <summary>Brings the current branch to include <paramref name="remote"/>. Does nothing when it already does.</summary>
    public void Run(Commit local, Commit remote, string branch)
    {
        var divergence = repo.ObjectDatabase.CalculateHistoryDivergence(local, remote);
        if (divergence.BehindBy == 0)
        {
            return;
        }

        var baseCommit = repo.ObjectDatabase.FindMergeBase(local, remote);
        var localFiles = Flatten(local.Tree);
        var remoteFiles = Flatten(remote.Tree);
        var baseFiles = Flatten(baseCommit?.Tree);

        // Files that are identical on both sides, or that Sync doesn't manage, end as the remote has them: no need to read them.
        var divergent = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in localFiles.Keys.Concat(remoteFiles.Keys).Concat(baseFiles.Keys))
        {
            if (localFiles.GetValueOrDefault(path)?.Id != remoteFiles.GetValueOrDefault(path)?.Id
                && managed.IsManaged(path))
            {
                divergent.Add(path);
            }
        }

        var tree = TreeDefinition.From(remote.Tree);
        var skippedFiles = FileMergePlan.Decide(
            divergent,
            path => new FileVersions(Bytes(baseFiles.GetValueOrDefault(path)), Bytes(localFiles.GetValueOrDefault(path)), Bytes(remoteFiles.GetValueOrDefault(path))),
            rules,
            contacts,
            (path, action) =>
            {
                if (action is Write write)
                {
                    tree.Add(path, repo.ObjectDatabase.CreateBlob(new MemoryStream(write.Content)), Mode.NonExecutableFile);
                }
                else
                {
                    tree.Remove(path);
                }
            });
        foreach (var file in skippedFiles)
        {
            skipped[file.RelativePath] = file;
        }

        var mergedTree = repo.ObjectDatabase.CreateTree(tree);
        Commit target = divergence.AheadBy == 0 && mergedTree.Id == remote.Tree.Id
            ? remote
            : CreateMergeCommit(mergedTree, local, remote, branch);
        FailOnLocalWork(Flatten(target.Tree));
        repo.Reset(ResetMode.Hard, target);
    }

    private Commit CreateMergeCommit(Tree tree, Commit local, Commit remote, string branch)
    {
        var author = new Signature(deviceName, "device@memoria.invalid", time.GetUtcNow());
        return repo.ObjectDatabase.CreateCommit(author, author, $"Sync from {deviceName}\n\nMerge origin/{branch}", tree, [local, remote], false);
    }

    // A hard reset overwrites every tracked file with the target's, so any uncommitted change to an unmanaged one would
    // be lost, whether or not the remote touched it. An untracked or ignored file survives unless the target needs its path.
    private void FailOnLocalWork(Dictionary<string, Blob> target)
    {
        var options = new StatusOptions { IncludeUntracked = true, RecurseUntrackedDirs = true, IncludeIgnored = true };
        foreach (var entry in repo.RetrieveStatus(options))
        {
            if ((entry.State & FileStatus.Unaltered) != 0
                || managed.IsManaged(entry.FilePath))
            {
                continue;
            }

            // An ignored folder is listed as one entry with a trailing slash.
            if (entry.State is FileStatus.NewInWorkdir or FileStatus.Ignored && !Obstructs(entry.FilePath.TrimEnd('/'), target))
            {
                continue;
            }

            throw new GitFailureException(SyncFailure.Repository, $"'{entry.FilePath}' has local work that the sync would overwrite.");
        }
    }

    // True when the target has the file's path, a file inside it, or a file at one of its parent folders.
    private static bool Obstructs(string path, Dictionary<string, Blob> target)
    {
        if (target.ContainsKey(path))
        {
            return true;
        }

        for (var slash = path.IndexOf('/', StringComparison.Ordinal); slash >= 0; slash = path.IndexOf('/', slash + 1))
        {
            if (target.ContainsKey(path[..slash]))
            {
                return true;
            }
        }

        var folder = path + "/";
        return target.Keys.Any(key => key.StartsWith(folder, StringComparison.Ordinal));
    }

    private static Dictionary<string, Blob> Flatten(Tree? tree)
    {
        var files = new Dictionary<string, Blob>(StringComparer.Ordinal);
        if (tree is not null)
        {
            Walk(tree, files);
        }

        return files;
    }

    private static void Walk(Tree tree, Dictionary<string, Blob> files)
    {
        foreach (var entry in tree)
        {
            switch (entry.Target)
            {
                case Tree child:
                    Walk(child, files);
                    break;
                case Blob blob:
                    files[entry.Path.Replace('\\', '/')] = blob;
                    break;
            }
        }
    }

    private static byte[]? Bytes(Blob? blob)
    {
        if (blob is null)
        {
            return null;
        }

        using var content = blob.GetContentStream();
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        return buffer.ToArray();
    }
}
