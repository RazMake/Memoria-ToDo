using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Lists the open tasks, newest first, for a snippet that links to one. What the user types after the list opens narrows
/// it by Tasks' search. Choosing a task inserts the snippet in place of the text from where it started.
/// </summary>
internal sealed class TaskReferencePicking
{
    private readonly ITasks _tasks;
    private readonly PopupList _list;
    private readonly PopupLists _popups;
    private StyledTextEditor? _field;
    private ISnippet? _snippet;
    private TextAnchor? _replaceFrom;
    private TextAnchor? _filterFrom;
    private IReadOnlyList<TaskItem> _shown = [];

    public TaskReferencePicking(ITasks tasks, PopupList list, PopupLists popups)
    {
        _tasks = tasks;
        _list = list;
        _popups = popups;
        list.Chosen += (_, position) => Choose(position);
    }

    /// <summary>Follows the field's edits, so the list narrows as the user types in it.</summary>
    public void Watch(StyledTextEditor field) =>
        field.WhenEditedOrMoved(() =>
        {
            if (_list.IsOpen && field == _field)
            {
                Update(field);
            }
        });

    /// <summary>Opens the list. The snippet replaces the text from the editor offset <paramref name="replaceFrom"/> to the end of the selection.</summary>
    public void Open(StyledTextEditor field, ISnippet snippet, int replaceFrom)
    {
        _field = field;
        _snippet = snippet;
        _replaceFrom = field.AnchorAt(replaceFrom, staysBefore: true);
        _filterFrom = field.AnchorAt(field.SelectionStart + field.SelectionLength, staysBefore: true);
        Update(field);
    }

    // Lists the matching open tasks, or closes the list when there are none or the cursor left what the user typed.
    private void Update(StyledTextEditor field)
    {
        if (_filterFrom is not { IsDeleted: false } from || field.CaretOffset < from.Offset)
        {
            _list.Close();
            return;
        }

        var filter = field.SelectionLength > 0 ? "" : field.Document.GetText(from.Offset, field.CaretOffset - from.Offset);
        _shown = [.. _tasks.Search(filter).Where(task => task.Resolved is null)];
        if (_shown.Count == 0)
        {
            _list.Close();
            return;
        }

        _popups.Show(_list, [.. _shown.Select(task => PlainTitle(task.Title))]);
    }

    private void Choose(int position)
    {
        var (field, snippet) = (_field, _snippet);
        if (field is null || snippet is null || _replaceFrom is not { IsDeleted: false } replaceFrom)
        {
            throw new InvalidOperationException("A task was chosen with no task list opened for a snippet.");
        }

        var task = _shown[position];
        var offset = replaceFrom.Offset;
        _list.Close();
        var input = new SnippetInput { Task = new TaskToReference(_tasks.LinkAddress(task.Id), task.Title) };
        SnippetInsertion.Apply(field, snippet, offset, input);
    }

    // The title as a reader sees it, without the Markdown marks, for the list. The link text keeps the raw title.
    private static string PlainTitle(string title) =>
        string.Concat(StyledSpans.Of(title, MarkdownField.Title)
            .Where(span => !span.Styles.HasFlag(SpanStyle.Marker))
            .Select(span => title.Substring(span.Start, span.Length)));
}
