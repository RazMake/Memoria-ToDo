namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// Each tag's place in the order both apps show tags in, kept in tags.json beside the tag's color and shared by every
/// device. Safe to call from any thread; uses the task store's lock. Tags are normalized with TagName.Normalize.
/// </summary>
public interface ITagOrder
{
    /// <summary>
    /// tags in the user's order: the tags with no place first, in ordinal order, then the placed
    /// tags by place, equal places in ordinal order. The tags are normalized and each repeat dropped. Every tag has no
    /// place when no repository is selected, or tags.json can't be read or has a newer version, which SkippedFiles names.
    /// Throws ArgumentException for a tag that's empty once normalized.
    /// </summary>
    IReadOnlyList<string> Sort(IReadOnlyCollection<string> tags);

    /// <summary>
    /// Gives tag a place after previousTag and before nextTag, as Sort shows them; null leaves that side open, so
    /// Move(tag, null, null) changes nothing. When a neighbor has no place, or both share one, first gives the tags in
    /// use, tag, and its neighbors places in the order Sort shows them. Writes tags.json with the current UTC time and
    /// normalized tag, then raises LocalSaved and Changed. Writes and raises nothing when tag already shows
    /// there. Throws, before writing: ArgumentException for a tag that's empty once normalized, a neighbor that is tag itself, or a
    /// previousTag that doesn't show before nextTag; NoRepositoryFolderException when no repository is selected; and
    /// InvalidDataException when tags.json can't be read or has a newer version.
    /// </summary>
    void Move(string tag, string? previousTag, string? nextTag);

    /// <summary>
    /// Raised on the calling thread, outside the store's lock, after a move here, on repository selection,
    /// and on every reload.
    /// </summary>
    event EventHandler? Changed;
}
