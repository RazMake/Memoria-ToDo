namespace Memoria.ToDo.Shared.Tasks;

/// <summary>
/// Each tag's color, kept in tags.json and shared by every device. Safe to call from any thread;
/// uses the task store's lock. Tags are normalized with TagName.Normalize. A color's name is kept without interpreting it.
/// SetColor throws NoticeFailedException of TaskItem, with a null Saved, when the color was saved and then a notice
/// handler or the local save report failed, even for one failure.
/// </summary>
public interface ITagColors
{
    /// <summary>
    /// The color name kept for tag, or null when there's none: no entry, no selected repository,
    /// or a tags.json that can't be read or has a newer version, which SkippedFiles names.
    /// </summary>
    string? ColorOf(string tag);

    /// <summary>
    /// Keeps colorName for tag, with the current UTC time, creating tags.json when it's missing, then raises
    /// LocalSaved and Changed. The same color as kept writes and raises nothing. Throws, before writing:
    /// ArgumentException for a tag that's empty once normalized, or a colorName that isn't one non-empty line;
    /// NoRepositoryFolderException when no repository is selected; and InvalidDataException when
    /// tags.json can't be read or has a newer version.
    /// </summary>
    void SetColor(string tag, string colorName);

    /// <summary>
    /// Raised on the calling thread, outside the store's lock, after a color is kept here,
    /// on repository selection, and on every reload.
    /// </summary>
    event EventHandler? Changed;
}
