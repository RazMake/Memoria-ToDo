using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>The contents of one path on each side of a merge; null means the side has no such file.</summary>
internal sealed record FileVersions(byte[]? Base, byte[]? Local, byte[]? Remote);

/// <summary>What the merge does to one path of the remote's tree.</summary>
internal abstract record PathAction;

/// <summary>The merged result has no such file, but the remote does.</summary>
internal sealed record Remove : PathAction;

/// <summary>The merged result has this content, and the remote's copy differs or is missing.</summary>
internal sealed record Write(byte[] Content) : PathAction;

/// <summary>
/// Decides each file of a merge from its base, local and remote contents, by the Tasks rules for its kind. Needs no Git:
/// a path that gets no action ends as the remote has it.
/// </summary>
internal static class FileMergePlan
{
    private static readonly Remove RemoveAction = new();

    /// <summary>
    /// Decides each path in turn and hands its action to <paramref name="apply"/> before reading the next, so only one file's
    /// contents are held at a time. Returns the files whose local copy was kept because they couldn't be merged.
    /// </summary>
    /// <param name="paths">Every path that exists on any side, each once.</param>
    /// <param name="read">Reads one path's contents on each side. Called only for a path that needs deciding.</param>
    public static IReadOnlyList<ISkippedRepositoryFile> Decide(IEnumerable<string> paths, Func<string, FileVersions> read, IRepositoryFileRules rules, Action<string, PathAction> apply)
    {
        var skipped = new List<ISkippedRepositoryFile>();

        // Tasks merge first, so an image can follow its task: a task one side deleted and the other edited comes back,
        // and takes along the images the editing side still has.
        var survivorIsLocal = new Dictionary<string, bool>(StringComparer.Ordinal);
        List<string>? images = null;
        foreach (var path in paths)
        {
            var kind = rules.Classify(path);
            if (kind == RepositoryFileKind.Image)
            {
                (images ??= []).Add(path);
                continue;
            }

            if (kind == RepositoryFileKind.Unmanaged)
            {
                continue;
            }

            var versions = read(path);
            if (SameContent(versions.Local, versions.Remote))
            {
                continue;
            }

            var merged = Merge(path, kind, versions, rules, skipped);
            if (merged is not null && versions.Base is not null && (versions.Local is null) != (versions.Remote is null) && kind == RepositoryFileKind.Task)
            {
                survivorIsLocal[rules.TaskIdInPath(path)!] = versions.Local is not null;
            }

            Record(apply, path, versions.Remote, merged);
        }

        if (images is not null)
        {
            foreach (var path in images)
            {
                var versions = read(path);
                if (SameContent(versions.Local, versions.Remote))
                {
                    continue;
                }

                var merged = Merge(path, RepositoryFileKind.Image, versions, rules, skipped);
                if (merged is null
                    && rules.TaskIdInPath(path) is { } owner
                    && survivorIsLocal.TryGetValue(owner, out var localSurvives))
                {
                    merged = localSurvives ? versions.Local : versions.Remote;
                }

                Record(apply, path, versions.Remote, merged);
            }
        }

        return skipped;
    }

    private static void Record(Action<string, PathAction> apply, string path, byte[]? remote, byte[]? merged)
    {
        if (merged is null)
        {
            if (remote is not null)
            {
                apply(path, RemoveAction);
            }
        }
        else if (!SameContent(merged, remote))
        {
            apply(path, new Write(merged));
        }
    }

    private static byte[]? Merge(string path, RepositoryFileKind kind, FileVersions versions, IRepositoryFileRules rules, List<ISkippedRepositoryFile> skipped)
    {
        var (b, l, r) = (versions.Base, versions.Local, versions.Remote);
        try
        {
            return FileMerges.Merge(kind, rules, path, versions);
        }
        catch (InvalidDataException)
        {
            // An unreadable file is never deleted or overwritten with the other side's guess: keep the side that changed.
            if (SameContent(b, r))
            {
                return l;
            }

            if (SameContent(b, l) || l is null)
            {
                return r;
            }

            // Both sides changed it. The one accepted loss: a malformed local task yields to the remote deleting it.
            if (r is null && kind == RepositoryFileKind.Task && !IsReadableTask(path, l, rules))
            {
                return null;
            }

            skipped.Add(new SkippedMergeFile(path, "This device and the remote both changed the file and one copy can't be read, so this device's copy was kept."));
            return l;
        }
    }

    // A task merged with itself is readable exactly when the file is a valid task for this path.
    private static bool IsReadableTask(string path, byte[] file, IRepositoryFileRules rules)
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

    internal static bool SameContent(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);

    private sealed record SkippedMergeFile(string RelativePath, string Reason) : ISkippedRepositoryFile
    {
        public bool RequiresUpdate => false;
    }
}
