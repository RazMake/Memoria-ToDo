using System.Windows;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The edit of a task's title in its row on the left. It leaves nothing behind: ending it unsaved drops what was typed, with no
/// Undo bar.
/// </summary>
internal sealed class TitleEdit : IFieldEdit
{
    private readonly ITasks _tasks;
    private readonly TitleEditor _editor;

    public TitleEdit(ITasks tasks, IReadOnlyList<PaletteColor> palette, ITheme theme, TimeProvider time, IClipboard clipboard,
        SaveKeys saveKeys, TaskItem task, Action saveRequested, Action cancelRequested)
    {
        _tasks = tasks;
        Started = task;
        _editor = new TitleEditor(tasks, palette, theme, time, clipboard, saveKeys) { Value = task.Title };
        _editor.SaveRequested += (_, _) => saveRequested();
        _editor.CancelRequested += (_, _) => cancelRequested();
    }

    public FrameworkElement Editor => _editor;

    public TaskItem Started { get; }

    public bool ShowsInRow => true;

    public bool HasChanges => _editor.HasChanges;

    public bool Save() => _editor.Save(EditSaves.Title(_tasks, Started));

    public void FocusEnd() => _editor.FocusEnd();

    public void Leave(EditCleanup cleanup, Action restore)
    {
    }

    public void Close(EditCleanup cleanup)
    {
    }
}
