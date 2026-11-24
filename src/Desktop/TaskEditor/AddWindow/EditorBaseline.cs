using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// What the editor held when its value was last set, to tell whether anything changed since. Tags are compared as
/// Tasks' edit compares them: ignoring case and order. Non-blank tag text typed and not yet added counts too.
/// </summary>
internal sealed class EditorBaseline
{
    private string _title = "";
    private string _description = "";
    private HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);

    public void Reset(TaskDraft draft)
    {
        _title = draft.Title;
        _description = draft.Description;
        _tags = new HashSet<string>(draft.Tags, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasChanges(string title, string description, IReadOnlyList<string> tags, string typedTags) =>
        title != _title
        || description != _description
        || !_tags.SetEquals(tags)
        || !string.IsNullOrWhiteSpace(typedTags);
}
