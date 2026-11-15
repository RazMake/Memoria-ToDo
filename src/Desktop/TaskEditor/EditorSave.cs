using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Saving what the editor holds: typed tag text becomes a tag first, an empty Title is marked instead of saved, and a
/// failure Tasks' saves are known to throw is shown, keeping the text.
/// </summary>
internal sealed class EditorSave
{
    private readonly Func<bool> _addTypedTags;
    private readonly Func<TaskDraft> _value;
    private readonly Action _markTitleMissing;
    private readonly Action _focusTitle;
    private readonly Action<string> _showMessage;

    public EditorSave(Func<bool> addTypedTags, Func<TaskDraft> value, Action markTitleMissing, Action focusTitle,
        Action<string> showMessage)
    {
        ArgumentNullException.ThrowIfNull(addTypedTags);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(markTitleMissing);
        ArgumentNullException.ThrowIfNull(focusTitle);
        ArgumentNullException.ThrowIfNull(showMessage);
        _addTypedTags = addTypedTags;
        _value = value;
        _markTitleMissing = markTitleMissing;
        _focusTitle = focusTitle;
        _showMessage = showMessage;
    }

    /// <summary>
    /// Adds typed tag text as a tag, then calls <paramref name="save"/> with the value, unless the Title is empty, which
    /// it marks and puts the cursor in. Returns false, saving nothing, when the typed tag couldn't be added, the Title is
    /// empty, or <paramref name="save"/> threw a failure Tasks' saves are known to throw, which it shows. Other failures
    /// propagate.
    /// </summary>
    public bool Run(Action<TaskDraft> save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (!_addTypedTags())
        {
            return false;
        }

        var draft = _value();
        if (string.IsNullOrWhiteSpace(draft.Title))
        {
            _markTitleMissing();
            _focusTitle();
            return false;
        }

        try
        {
            save(draft);
        }
        catch (Exception e) when (SaveFailures.MessageOf(e) is not null)
        {
            _showMessage(SaveFailures.MessageOf(e)!);
            return false;
        }

        _showMessage("");
        return true;
    }
}
