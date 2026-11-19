using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Contacts;
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
    private readonly IContacts _contacts;
    private readonly ChoiceList _list;
    private readonly PopupLists _popups;
    private ITextField? _field;
    private ISnippet? _snippet;
    private TextAnchor? _replaceFrom;
    private TextAnchor? _filterFrom;
    private IReadOnlyList<TaskItem> _shown = [];

    public TaskReferencePicking(ITasks tasks, IContacts contacts, ChoiceList list, PopupLists popups)
    {
        _tasks = tasks;
        _contacts = contacts;
        _list = list;
        _popups = popups;
        list.Chosen += Choose;
    }

    /// <summary>Follows the field's edits, so the list narrows as the user types in it.</summary>
    public void Watch(ITextField field) =>
        field.Changed += () =>
        {
            if (_list.IsOpen && field == _field)
            {
                Update(field);
            }
        };

    /// <summary>Opens the list. The snippet replaces the text from the editor offset <paramref name="replaceFrom"/> to the end of the selection.</summary>
    public void Open(ITextField field, ISnippet snippet, int replaceFrom)
    {
        _field = field;
        _snippet = snippet;
        _replaceFrom = field.AnchorAt(replaceFrom, staysBefore: true);
        _filterFrom = field.AnchorAt(field.SelectionStart + field.SelectionLength, staysBefore: true);
        Update(field);
    }

    // Lists the matching open tasks, or closes the list when there are none or the cursor left what the user typed.
    private void Update(ITextField field)
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

        _popups.Show(_list, [.. _shown.Select(task => Shown(task.Title))]);
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
        var input = new SnippetInput { Task = new TaskToReference(_tasks.LinkAddress(task.Id), PlainMentions(task.Title)) };
        SnippetInsertion.Apply(field, snippet, offset, input);
    }

    // The title with each mention as its live (or stored) text, plain; the reference text is escaped like the rest of the text.
    private string PlainMentions(string title)
    {
        var text = new System.Text.StringBuilder(title);
        foreach (var mention in Mentions.In(title, true).Reverse())
        {
            text.Remove(mention.Start, mention.Length).Insert(mention.Start, MentionChips.TextOf(_contacts, mention));
        }

        return text.ToString();
    }

    // The title as a reader sees it, with live mention texts (the stored Markdown when Contacts has none) and without the Markdown marks, for the list.
    private string Shown(string title) =>
        PlainTitle(Mentions.WithTexts(title, true, mention => _contacts.TextOf(mention.Address, mention.Template)));

    private static string PlainTitle(string title) =>
        string.Concat(StyledSpans.Of(title, MarkdownField.Title)
            .Where(span => !span.Styles.HasFlag(SpanStyle.Marker))
            .Select(span => title.Substring(span.Start, span.Length)));
}
