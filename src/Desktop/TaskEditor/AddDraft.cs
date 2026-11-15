using System.IO;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The Add window's draft: restored when the window opens, kept when it hides, cleared on request, and replaced by a new
/// one when the task is saved. A kept draft that can't be read stays on disk until the user types something to replace it.
/// </summary>
internal sealed class AddDraft
{
    private const string UnreadableMessage = "The kept draft couldn't be loaded, so Add opened empty.";

    private readonly ITasks _tasks;
    private readonly IDraftEditor _editor;
    private bool _unreadableDraftOnDisk;

    public AddDraft(ITasks tasks, IDraftEditor editor)
    {
        _tasks = tasks;
        _editor = editor;
    }

    /// <summary>Fills the editor with the kept draft, or a new one when there is none or it can't be read.</summary>
    public void Restore()
    {
        // The window stays alive while hidden, so Tags text typed and not yet added is still in its line; setting the draft clears it.
        var typedTags = _editor.TypedTags;
        try
        {
            _editor.Value = _tasks.LoadDraft() ?? _tasks.StartDraft();
            _unreadableDraftOnDisk = false;
            _editor.ShowMessage("");
        }
        catch (InvalidDataException)
        {
            _editor.Value = _tasks.StartDraft();
            _unreadableDraftOnDisk = true;
            _editor.ShowMessage(UnreadableMessage);
        }

        _editor.TypedTags = typedTags;
    }

    /// <summary>Keeps what the editor holds as the draft, unless that would replace an unreadable one with nothing.</summary>
    public void Keep()
    {
        var draft = _editor.Value;
        if (!_unreadableDraftOnDisk || !IsUntouched(draft))
        {
            _tasks.KeepDraft(draft);
            _unreadableDraftOnDisk = false;
        }
    }

    /// <summary>Discards the kept draft, starts a new one, and puts the cursor in the Title.</summary>
    public void Clear()
    {
        _tasks.DiscardDraft();
        StartNew();
        _editor.FocusTitle();
    }

    /// <summary>
    /// Adds the editor's task and starts a new draft. Returns true when the window should hide, which is after a save
    /// that doesn't <paramref name="keepOpen"/>; false when nothing was saved or the window stays open.
    /// </summary>
    public bool Save(bool keepOpen)
    {
        if (!_editor.Save(draft => _tasks.Add(draft)))
        {
            return false;
        }

        StartNew();
        if (keepOpen)
        {
            _editor.FocusTitle();
            return false;
        }

        return true;
    }

    private static bool IsUntouched(TaskDraft draft) =>
        draft.Title.Length == 0 && draft.Description.Length == 0 && draft.Tags.Count == 0;

    private void StartNew()
    {
        _editor.Value = _tasks.StartDraft();
        _unreadableDraftOnDisk = false;
        _editor.ShowMessage("");
    }
}
