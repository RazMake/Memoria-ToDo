using Memoria.ToDo.Shared.Tasks;
using Serilog;

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
    private readonly ILogger _logger;

    public EditorSave(Func<bool> addTypedTags, Func<TaskDraft> value, Action markTitleMissing, Action focusTitle,
        Action<string> showMessage, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
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
        _logger = logger;
    }

    /// <summary>
    /// Adds typed tag text as a tag, then calls <paramref name="save"/> with the value, unless the Title is empty, which
    /// it marks and puts the cursor in. Returns false when the typed tag couldn't be added or the Title is empty, saving
    /// nothing, or when <paramref name="save"/> threw a failure Tasks' saves are known to throw, which it shows; after several
    /// failures at once the change may be partly saved. A save kept before a notice failed returns true, with the shared
    /// message shown. Other failures propagate.
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

        return SaveFailures.Try(save, draft, _showMessage, _logger);
    }
}
