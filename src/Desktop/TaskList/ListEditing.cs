using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The one open edit of a task's title, in its row, or of its description, on the right side. It ends by saving, or by leaving
/// without saving, where a changed description can be brought back with Undo.
/// </summary>
internal sealed class ListEditing(ITasks tasks, IContacts contacts, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time,
    IClipboard clipboard, IImageFileChooser imageFiles, IFileSystem files, SaveKeys saveKeys, EditCleanup cleanup, Action<string> restored, ILogger logger)
{
    private IFieldEdit? _edit;

    /// <summary>The open edit, or null.</summary>
    public IFieldEdit? Current => _edit;

    /// <summary>The open edit when it is of a description, which has a draft to preview, else null.</summary>
    public IDescriptionEdit? Describing => _edit as IDescriptionEdit;

    /// <summary>The task as it was when the open edit began, or null when none is open.</summary>
    public TaskItem? Started => _edit?.Started;

    /// <summary>Opens an edit of the task's description, with the editor's Save and Esc calling the given actions.</summary>
    public IFieldEdit BeginDescription(TaskItem task, Action saveRequested, Action cancelRequested) =>
        _edit = new DescriptionEdit(tasks, contacts, palette, theme, time, clipboard, imageFiles, files, saveKeys, task, saveRequested, cancelRequested, logger);

    /// <summary>Opens an edit of the task's title, with the editor's Save and Esc calling the given actions.</summary>
    public IFieldEdit BeginTitle(TaskItem task, Action saveRequested, Action cancelRequested) =>
                _edit = new TitleEdit(tasks, contacts, palette, theme, time, clipboard, saveKeys, task, saveRequested, cancelRequested, logger);

    /// <summary>
    /// Saves the edit through Tasks and ends it. False, with the edit still open, when the field is empty or Tasks can't save it;
    /// the editor says why.
    /// </summary>
    public bool Save()
    {
        if (!_edit!.Save())
        {
            return false;
        }

        _edit = null;
        return true;
    }

    /// <summary>
    /// Ends the edit without saving. A clean description throws its images away now; a changed one waits on the Undo bar, where
    /// Undo opens the same editor again. A title edit just ends.
    /// </summary>
    public void Leave()
    {
        var edit = _edit!;
        _edit = null;
        edit.Leave(cleanup, () =>
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

    /// <summary>Drops the open edit with no cleanup, and returns it, or null when none was open.</summary>
    public IFieldEdit? Forget()
    {
        var edit = _edit;
        _edit = null;
        return edit;
    }

    /// <summary>Whether the element is the editor or inside it.</summary>
    public bool Contains(object source) =>
        _edit is { } edit && source is DependencyObject element && (edit.Editor == element || edit.Editor.IsAncestorOf(element));

    // With the focus outside the editor, Esc still takes its steps: the editor gets the key as if the focus were in it, and closes
    // its open list or reports that the edit is cancelled. The key the editor is sent is handled in the editor, so the window's
    // own Esc never sees it.
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
