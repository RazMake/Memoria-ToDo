using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Sync;

/// <summary>Merges one path's base, local and remote contents; null means the merged result has no such file.</summary>
internal delegate byte[]? FileMerge(IRepositoryFileRules rules, IContactFileRules contacts, string path, byte[]? @base, byte[]? local, byte[]? remote);

/// <summary>
/// Picks the merge for each kind of managed file from Tasks' and Contacts' file rules. A new kind of synced file adds one entry here.
/// A kind with no entry, such as an image, keeps the side that changed, and the remote's copy when both did.
/// </summary>
internal static class FileMerges
{
    private static readonly Dictionary<RepositoryFileKind, FileMerge> ByKind = new()
    {
        [RepositoryFileKind.Task] = (rules, _, path, b, l, r) => rules.MergeTask(rules.TaskIdInPath(path)!, b, l, r),
        [RepositoryFileKind.Settings] = (rules, _, _, b, l, r) => rules.MergeSettings(b, l, r),
        [RepositoryFileKind.TagColors] = (rules, _, _, b, l, r) => rules.MergeTagColors(b, l, r),
        // Tasks doesn't manage a contact file, so the caller asks for this entry by the kind Tasks gave the path.
        [RepositoryFileKind.Unmanaged] = (_, contacts, path, b, l, r) => contacts.Merge(path, b, l, r),
    };

    public static byte[]? Merge(RepositoryFileKind kind, IRepositoryFileRules rules, IContactFileRules contacts, string path, FileVersions versions)
    {
        var (b, l, r) = (versions.Base, versions.Local, versions.Remote);
        if (ByKind.TryGetValue(kind, out var merge))
        {
            return merge(rules, contacts, path, b, l, r);
        }

        return FileMergePlan.SameContent(b, l) ? r : FileMergePlan.SameContent(b, r) ? l : r;
    }
}
