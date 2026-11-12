using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>A file both the Git pass and the Tasks reload skipped: the merge's reason, plus the reload's newer-version hint.</summary>
internal sealed record MergedSkippedFile(string RelativePath, string Reason, bool RequiresUpdate) : ISkippedRepositoryFile
{
    /// <summary>One entry per path. The first list's reason wins; <see cref="ISkippedRepositoryFile.RequiresUpdate"/> is true if either list says so.</summary>
    public static List<ISkippedRepositoryFile> Combine(
        IEnumerable<ISkippedRepositoryFile> preferred, IEnumerable<ISkippedRepositoryFile> other)
    {
        var combined = new List<ISkippedRepositoryFile>();
        var indexByPath = new Dictionary<string, int>();
        foreach (var file in preferred.Concat(other))
        {
            if (!indexByPath.TryGetValue(file.RelativePath, out var index))
            {
                indexByPath[file.RelativePath] = combined.Count;
                combined.Add(file);
            }
            else if (file.RequiresUpdate && !combined[index].RequiresUpdate)
            {
                combined[index] = new MergedSkippedFile(file.RelativePath, combined[index].Reason, true);
            }
        }

        return combined;
    }
}
