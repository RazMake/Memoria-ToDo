using LibGit2Sharp;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>
/// Joins the local branch with the fetched remote tip. Each file is merged by the Tasks rules for its kind; files
/// Sync doesn't manage stay as the remote has them.
/// </summary>
internal sealed class DomainMerge(Repository repo, IRepositoryFileRules rules, TimeProvider time, string deviceName)
{
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

        var tree = TreeDefinition.From(remote.Tree);
        var paths = new HashSet<string>(localFiles.Keys);
        paths.UnionWith(remoteFiles.Keys);
        paths.UnionWith(baseFiles.Keys);

        // Tasks merge first, so an image can follow its task: a task one side deleted and the other edited comes back,
        // and takes along the images the editing side still has.
        var survivorIsLocal = new Dictionary<string, bool>(StringComparer.Ordinal);
        var images = new List<string>();
        foreach (var path in paths)
        {
            if (rules.Classify(path) == RepositoryFileKind.Image)
            {
                images.Add(path);
                continue;
            }

            localFiles.TryGetValue(path, out var l);
            remoteFiles.TryGetValue(path, out var r);
            baseFiles.TryGetValue(path, out var b);
            var merged = l?.Id == r?.Id ? null : Merge(path, b, l, r);
            if (merged is not null && b is not null && (l is null) != (r is null) && rules.Classify(path) == RepositoryFileKind.Task)
            {
                survivorIsLocal[rules.TaskIdInPath(path)!] = l is not null;
            }

            Apply(path, l, r, merged);
        }

        foreach (var path in images)
        {
            localFiles.TryGetValue(path, out var l);
            remoteFiles.TryGetValue(path, out var r);
            baseFiles.TryGetValue(path, out var b);
            var merged = l?.Id == r?.Id ? null : Merge(path, b, l, r);
            if (merged is null
                && rules.TaskIdInPath(path) is { } owner
                && survivorIsLocal.TryGetValue(owner, out var localSurvives)
                && (localSurvives ? l : r) is { } kept)
            {
                merged = Bytes(kept);
            }

            Apply(path, l, r, merged);
        }

        void Apply(string path, Blob? l, Blob? r, byte[]? merged)
        {
            if (l?.Id == r?.Id)
            {
                return;
            }

            if (merged is null)
            {
                tree.Remove(path);
            }
            else if (r is null || !merged.SequenceEqual(Bytes(r)))
            {
                tree.Add(path, repo.ObjectDatabase.CreateBlob(new MemoryStream(merged)), Mode.NonExecutableFile);
            }
        }

        var mergedTree = repo.ObjectDatabase.CreateTree(tree);
        Commit target = divergence.AheadBy == 0 && mergedTree.Id == remote.Tree.Id
            ? remote
            : CreateMergeCommit(mergedTree, local, remote, branch);
        FailOnLocalWork(Flatten(target.Tree));
        repo.Reset(ResetMode.Hard, target);
    }

    private byte[]? Merge(string path, Blob? basis, Blob? local, Blob? remote)
    {
        var kind = rules.Classify(path);
        if (kind == RepositoryFileKind.Unmanaged)
        {
            return remote is null ? null : Bytes(remote);
        }

        var (b, l, r) = (basis is null ? null : Bytes(basis), local is null ? null : Bytes(local), remote is null ? null : Bytes(remote));
        try
        {
            return kind switch
            {
                RepositoryFileKind.Task => rules.MergeTask(rules.TaskIdInPath(path)!, b, l, r),
                RepositoryFileKind.Settings => rules.MergeSettings(b, l, r),
                _ => basis?.Id == local?.Id ? r : basis?.Id == remote?.Id ? l : r,
            };
        }
        catch (InvalidDataException)
        {
            // An unreadable file is never deleted or overwritten with the other side's guess: keep the side that changed.
            if (basis?.Id == remote?.Id)
            {
                return l;
            }

            if (basis?.Id == local?.Id || l is null)
            {
                return r;
            }

            // Both sides changed it. The one accepted loss: a malformed local task yields to the remote deleting it.
            if (r is null && kind == RepositoryFileKind.Task && !IsReadableTask(path, l))
            {
                return null;
            }

            skipped[path] = new SkippedMergeFile(path, "This device and the remote both changed the file and one copy can't be read, so this device's copy was kept.");
            return l;
        }
    }

    // A task merged with itself is readable exactly when the file is a valid task for this path.
    private bool IsReadableTask(string path, byte[] file)
    {
        try
        {
            rules.MergeTask(rules.TaskIdInPath(path)!, null, file, file);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private sealed record SkippedMergeFile(string RelativePath, string Reason) : ISkippedRepositoryFile
    {
        public bool RequiresUpdate => false;
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
                || rules.Classify(entry.FilePath) != RepositoryFileKind.Unmanaged)
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

    private static byte[] Bytes(Blob blob)
    {
        using var content = blob.GetContentStream();
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        return buffer.ToArray();
    }
}
