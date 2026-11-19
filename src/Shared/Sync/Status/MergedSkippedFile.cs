using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>A file both the Git pass and the repository's reload skipped: the merge's reason, plus the reload's newer-version hint.</summary>
internal sealed record MergedSkippedFile(string RelativePath, string Reason, bool RequiresUpdate) : ISkippedRepositoryFile
{
    /// <summary>
    /// One entry per path, in the reload's order (Tasks' files before Contacts'), then the files only the merge skipped.
    /// The merge's reason wins; <see cref="ISkippedRepositoryFile.RequiresUpdate"/> is true if either list says so.
    /// </summary>
    public static List<ISkippedRepositoryFile> Combine(
        IEnumerable<ISkippedRepositoryFile> merge, IEnumerable<ISkippedRepositoryFile> reload)
    {
        var combined = new List<ISkippedRepositoryFile>();
        var indexByPath = new Dictionary<string, int>();
        Add(reload, false);
        Add(merge, true);
        return combined;

        void Add(IEnumerable<ISkippedRepositoryFile> files, bool isMerge)
        {
            foreach (var file in files)
            {
                if (!indexByPath.TryGetValue(file.RelativePath, out var index))
                {
                    indexByPath[file.RelativePath] = combined.Count;
                    combined.Add(file);
                    continue;
                }

                var current = combined[index];
                var reason = isMerge ? file.Reason : current.Reason;
                var requiresUpdate = current.RequiresUpdate || file.RequiresUpdate;
                if (reason != current.Reason || requiresUpdate != current.RequiresUpdate)
                {
                    combined[index] = new MergedSkippedFile(file.RelativePath, reason, requiresUpdate);
                }
            }
        }
    }
}
