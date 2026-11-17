namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// Each tag's place in the order both apps show tags in, kept in tags.json beside the tag's color and shared by every
/// device. Safe to call from any thread; uses the task store's lock. Tags match with case ignored.
/// </summary>
public interface ITagOrder
{
    /// <summary>
    /// tags in the user's order: the tags with no place first, in alphabetical order ignoring case, then the placed
    /// tags by place, equal places in alphabetical order ignoring case. Each tag keeps its spelling. Every tag has no
    /// place when no repository is selected, or tags.json can't be read or has a newer version, which SkippedFiles names.
    /// Throws ArgumentException for an invalid tag, or the same tag twice, ignoring case.
    /// </summary>
    IReadOnlyList<string> Sort(IReadOnlyCollection<string> tags);

    /// <summary>
    /// Gives tag a place after previousTag and before nextTag, as Sort shows them; null leaves that side open, so
    /// Move(tag, null, null) changes nothing. When a neighbor has no place, or both share one, first gives the tags in
    /// use, tag, and its neighbors places in the order Sort shows them. Writes tags.json with the current UTC time and
    /// tag as spelled in the call, then raises LocalSaved and Changed. Writes and raises nothing when tag already shows
    /// there. Throws, before writing: ArgumentException for an invalid tag, a neighbor that is tag itself, or a
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
