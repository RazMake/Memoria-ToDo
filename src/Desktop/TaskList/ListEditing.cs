using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The edit of one task's description on the right side: the whole Task editor, opened on the task as the list shows it. It
/// ends by saving, or by leaving without saving, where a changed edit can be brought back with Undo.
/// </summary>
internal sealed class ListEditing(ITasks tasks, ITagColors tagColors, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
    IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, EditCleanup cleanup, Action<string> restored)
{
    private sealed record Edit(TaskEditorControl Editor, TaskItem Started);

    private Edit? _edit;

    /// <summary>The editor while an edit is open, or null.</summary>
    public TaskEditorControl? Editor => _edit?.Editor;

    /// <summary>The task as it was when the open edit began, or null when none is open.</summary>
    public TaskItem? Started => _edit?.Started;

    /// <summary>Opens an edit of the task, with the editor's Save and Esc calling the given actions.</summary>
    public TaskEditorControl Begin(TaskItem task, Action saveRequested, Action cancelRequested)
    {
        var draft = new TaskDraft(task.Id, task.Title, task.Description, task.Tags);
        var editor = new TaskEditorControl(tasks, tagColors, palette, theme, time, clipboard, imageFiles, files) { Value = draft };
        editor.SaveRequested += (_, _) => saveRequested();
        editor.SaveAndClearRequested += (_, _) => saveRequested();
        editor.CancelRequested += (_, _) => cancelRequested();
        _edit = new Edit(editor, task);
        return editor;
    }

    /// <summary>
    /// Saves the edit through Tasks and ends it. False, with the edit still open, when the Title is empty or Tasks can't save it;
    /// the editor says why.
    /// </summary>
    public bool Save()
    {
        var edit = _edit!;
        if (!edit.Editor.Save(draft => tasks.SaveEdit(edit.Started, draft)))
        {
            return false;
        }

        _edit = null;
        return true;
    }

    /// <summary>
    /// Ends the edit without saving. A clean one throws its images away now; a changed one waits on the Undo bar, where Undo
    /// opens the same editor again.
    /// </summary>
    public void Leave()
    {
        var edit = _edit!;
        _edit = null;
        cleanup.Leave(edit.Started.Id, edit.Editor.HasChanges, () =>
        {
            // An edit open now is left as Esc leaves it, with its own Undo bar; this one's bar was just used.
            if (_edit is not null)
            {
                Leave();
            }

            _edit = edit;
            restored(edit.Started.Id);
        });
    }

    /// <summary>Drops the open edit with no cleanup, and returns its task, or null when none was open.</summary>
    public TaskItem? Forget()
    {
        var started = _edit?.Started;
        _edit = null;
        return started;
    }

    /// <summary>Whether the element is the editor or inside it.</summary>
    public bool Contains(object source) =>
        _edit is { } edit && source is DependencyObject element && (edit.Editor == element || edit.Editor.IsAncestorOf(element));

    // With the focus outside the editor, Esc still takes its steps: the editor gets the key as if the focus were in it, and closes
    // its open list or reports that the edit is cancelled. The key the editor is sent is handled in the editor, so the window's
    // own Esc never sees it, and the editor's tag control, which also listens at the window, sees it handled.
    /// <summary>Gives the editor the Esc key that arrived elsewhere, and says whether it handled it.</summary>
    public bool SendEsc(KeyEventArgs original)
    {
        if (_edit is not { } edit || PresentationSource.FromVisual(edit.Editor) is not { } source)
        {
            return false;
        }

        var sent = new KeyEventArgs(original.KeyboardDevice, source, original.Timestamp, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        edit.Editor.RaiseEvent(sent);
        return sent.Handled;
    }
}
