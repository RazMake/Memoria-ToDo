using ICSharpCode.AvalonEdit.Document;
using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Typing "/" in the Description opens the snippet list. It narrows as the user types, closes when the "/" no longer
/// opens Editing's picker, and choosing a snippet inserts it in place of the "/" and what was typed after it.
/// </summary>
internal sealed class SnippetPicking
{
    private readonly StyledTextEditor _field;
    private readonly PopupList _list;
    private readonly PopupLists _popups;
    private readonly SnippetInserter _inserter;
    private TextAnchor? _slash;
    private IReadOnlyList<ISnippet> _shown = [];

    public SnippetPicking(StyledTextEditor field, PopupList list, PopupLists popups, SnippetInserter inserter)
    {
        _field = field;
        _list = list;
        _popups = popups;
        _inserter = inserter;
        field.TextArea.TextEntered += (_, e) =>
        {
            if (e.Text == "/")
            {
                Open();
            }
        };
        field.WhenEditedOrMoved(() =>
        {
            if (_list.IsOpen)
            {
                Update();
            }
        });
        list.Chosen += (_, position) => Choose(position);
    }

    private void Open()
    {
        var slash = _field.CaretOffset - 1;
        if (SnippetPicker.OpensAt(_field.Current().Text, _field.LfOffset(slash)))
        {
            _slash = _field.AnchorAt(slash, staysBefore: false);
            Update();
        }
    }

    // Lists the snippets that match, or closes the list when there are none or the "/" no longer opens the picker.
    private void Update()
    {
        if (_slash is not { IsDeleted: false } slash)
        {
            Dismiss();
            return;
        }

        var field = _field.Current();
        var index = _field.LfOffset(slash.Offset);
        if (field.SelectionStart <= index || !SnippetPicker.OpensAt(field.Text, index))
        {
            Dismiss();
            return;
        }

        _shown = [.. SnippetPicker.Matching(field, index)];
        if (_shown.Count == 0)
        {
            Dismiss();
            return;
        }

        _popups.Show(_list, [.. _shown.Select(snippet => snippet.Name)]);
    }

    private void Dismiss()
    {
        _slash = null;
        _list.Close();
    }

    private void Choose(int position)
    {
        var snippet = _shown[position];
        var slash = _slash?.Offset ?? throw new InvalidOperationException("A snippet was chosen with no \"/\" typed.");
        Dismiss();
        _inserter.Insert(_field, snippet, slash);
    }
}
